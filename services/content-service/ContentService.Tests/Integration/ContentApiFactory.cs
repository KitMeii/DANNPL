using ContentService.Api.Data;
using ContentService.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContentService.Tests.Integration;

/// <summary>
/// Backs <see cref="ContentDbContext"/> with a fresh, isolated database per factory instance (one
/// per test class, via IClassFixture). Two modes, chosen by the presence of the
/// TEST_MSSQL_CONNECTION env var — see AuthApiFactory for the full rationale:
///  - Unset (default, local dev): EF Core InMemory.
///  - Set (CI, see content-service.yml's mssql service container): a real SQL Server database
///    named content_tests_&lt;guid&gt;, migrated in InitializeAsync and dropped in DisposeAsync.
/// </summary>
public sealed class ContentApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string? SqlServerBaseConnectionString =
        Environment.GetEnvironmentVariable("TEST_MSSQL_CONNECTION");

    private readonly string _databaseName = $"content_tests_{Guid.NewGuid():N}";
    public readonly FakeFileStorage FileStorage = new();

    private bool UseSqlServerBackend => !string.IsNullOrWhiteSpace(SqlServerBaseConnectionString);

    private string BuildSqlServerConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(SqlServerBaseConnectionString) { InitialCatalog = _databaseName };
        return builder.ConnectionString;
    }

    // Program.cs reads ConnectionStrings:ContentDb and Jwt:SigningKey off the default config
    // providers (env vars, appsettings.json) before ConfigureWebHost below ever runs — on a
    // checkout without a local appsettings.Development.json (gitignored; every CI run included),
    // it throws before we get a chance to configure anything, regardless of which backend ends up
    // being used. Seed both as process env vars, only if not already set. The connection string
    // value is irrelevant when UseSqlServerBackend is false (always replaced below) and unused
    // entirely when true; the signing key must match TestTokens.cs.
    static ContentApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ContentDb",
            Environment.GetEnvironmentVariable("ConnectionStrings__ContentDb") ?? "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            Environment.GetEnvironmentVariable("Jwt__SigningKey") ?? "dev-only-signing-key-do-not-use-in-production-min-32-chars");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:AutoMigrate", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ContentDbContext>>();
            services.AddDbContext<ContentDbContext>(options =>
            {
                if (UseSqlServerBackend)
                {
                    options.UseSqlServer(BuildSqlServerConnectionString());
                }
                else
                {
                    options.UseInMemoryDatabase(_databaseName);
                }
            });

            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(FileStorage);
        });
    }

    public async Task InitializeAsync()
    {
        if (!UseSqlServerBackend)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        if (UseSqlServerBackend)
        {
            await using var connection = new SqlConnection(SqlServerBaseConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}];";
            await command.ExecuteNonQueryAsync();
        }

        await base.DisposeAsync();
    }
}
