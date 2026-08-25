using AuthService.Api.Data;
using AuthService.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuthService.Tests.Integration;

/// <summary>
/// Backs <see cref="AuthDbContext"/> with a fresh, isolated database per factory instance (one per
/// test class, via IClassFixture) so integration tests exercise the real HTTP pipeline (auth,
/// validation, exception middleware, endpoints) without cross-test data bleed.
///
/// Two modes, chosen by the presence of the TEST_MSSQL_CONNECTION env var:
///  - Unset (default, local dev): EF Core InMemory — fast, no external dependency.
///  - Set (CI, see auth-service.yml's mssql service container): a real SQL Server database named
///    auth_tests_&lt;guid&gt;, migrated in InitializeAsync and dropped in DisposeAsync. This is what
///    actually exercises SQL Server-specific behavior (decimal precision, nvarchar, real
///    constraints) that InMemory silently ignores or fakes.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string? SqlServerBaseConnectionString =
        Environment.GetEnvironmentVariable("TEST_MSSQL_CONNECTION");

    private readonly string _databaseName = $"auth_tests_{Guid.NewGuid():N}";

    /// <summary>Value RequireInternalServiceKeyFilter expects on GET /users and PUT
    /// /users/{id}/role — tests simulating the legitimate admin-service caller must send this via
    /// the X-Internal-Key header; see AdminUserManagementTests.</summary>
    public const string TestInternalServiceKey = "dev-only-internal-key-do-not-use-in-production";

    // Program.cs reads ConnectionStrings:AuthDb, Jwt:SigningKey and InternalService:SharedKey off
    // the default config providers (env vars, appsettings.json) before ConfigureWebHost below
    // ever runs — on a checkout without a local appsettings.Development.json (gitignored; every
    // CI run included), it throws before this factory gets a chance to configure anything,
    // regardless of which backend (SQL Server or InMemory) ends up being used. Seed all three as
    // process env vars, only if not already set. The connection string value here is irrelevant
    // when UseSqlServerBackend is false (always replaced below) and unused entirely when it's
    // true (BuildSqlServerConnectionString takes over); the signing key must match every other
    // service's TestTokens.cs.
    static AuthApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__AuthDb",
            Environment.GetEnvironmentVariable("ConnectionStrings__AuthDb") ?? "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            Environment.GetEnvironmentVariable("Jwt__SigningKey") ?? "dev-only-signing-key-do-not-use-in-production-min-32-chars");
        Environment.SetEnvironmentVariable("InternalService__SharedKey",
            Environment.GetEnvironmentVariable("InternalService__SharedKey") ?? TestInternalServiceKey);
    }

    /// <summary>Set by each test right after CreateClient() — public so tests can read
    /// UploadCallCount/LastDeletedPublicId without re-resolving from DI.</summary>
    public FakeAvatarStorage AvatarStorage { get; } = new();

    private bool UseSqlServerBackend => !string.IsNullOrWhiteSpace(SqlServerBaseConnectionString);

    private string BuildSqlServerConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(SqlServerBaseConnectionString) { InitialCatalog = _databaseName };
        return builder.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Migration/seeding is driven explicitly below (InitializeAsync), not by the app's own
        // startup path, and tests don't want the demo-account seeder polluting a fresh database.
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("Seed:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.AddDbContext<AuthDbContext>(options =>
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

            services.RemoveAll<IAvatarStorage>();
            services.AddSingleton<IAvatarStorage>(AvatarStorage);
        });
    }

    public async Task InitializeAsync()
    {
        if (!UseSqlServerBackend)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        if (UseSqlServerBackend)
        {
            await using var connection = new SqlConnection(SqlServerBaseConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            // Kick other connections off first — SQL Server refuses to DROP DATABASE while the
            // pool this test run opened is still attached.
            command.CommandText =
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}];";
            await command.ExecuteNonQueryAsync();
        }

        await base.DisposeAsync();
    }
}
