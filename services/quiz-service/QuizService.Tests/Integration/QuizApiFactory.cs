using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizService.Api.Clients;
using QuizService.Api.Data;
using QuizService.Api.Grading;
using QuizService.Api.Progress;

namespace QuizService.Tests.Integration;

/// <summary>
/// Backs <see cref="QuizDbContext"/> with a fresh, isolated database per factory instance (one
/// per test class, via IClassFixture). Two modes, chosen by the presence of the
/// TEST_MSSQL_CONNECTION env var — see AuthApiFactory for the full rationale:
///  - Unset (default, local dev): EF Core InMemory.
///  - Set (CI, see quiz-service.yml's mssql service container): a real SQL Server database
///    named quiz_tests_&lt;guid&gt;, migrated in InitializeAsync and dropped in DisposeAsync.
/// </summary>
public sealed class QuizApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string? SqlServerBaseConnectionString =
        Environment.GetEnvironmentVariable("TEST_MSSQL_CONNECTION");

    private readonly string _databaseName = $"quiz_tests_{Guid.NewGuid():N}";
    public readonly FakeProgressReporter ProgressReporter = new();
    public readonly AuthQuizClientSeed AuthQuizClient = new();

    /// <summary>Value RequireInternalServiceKeyFilter expects on POST /stats/scores-by-users —
    /// tests simulating the legitimate admin-service caller must send this via the X-Internal-Key
    /// header; see AuthApiFactory.TestInternalServiceKey for the identical pattern in auth-service.</summary>
    public const string TestInternalServiceKey = "dev-only-internal-key-do-not-use-in-production";

    private bool UseSqlServerBackend => !string.IsNullOrWhiteSpace(SqlServerBaseConnectionString);

    private string BuildSqlServerConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(SqlServerBaseConnectionString) { InitialCatalog = _databaseName };
        return builder.ConnectionString;
    }

    // Program.cs reads ConnectionStrings:QuizDb and Jwt:SigningKey off the default config
    // providers (env vars, appsettings.json) before ConfigureWebHost below ever runs — on a
    // checkout without a local appsettings.Development.json (gitignored; every CI run included),
    // it throws before we get a chance to configure anything, regardless of which backend ends up
    // being used. Seed both as process env vars, only if not already set. The connection string
    // value is irrelevant when UseSqlServerBackend is false (always replaced below) and unused
    // entirely when true; the signing key must match TestTokens.cs.
    static QuizApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__QuizDb",
            Environment.GetEnvironmentVariable("ConnectionStrings__QuizDb") ?? "Server=unused;Database=unused;Trusted_Connection=True;TrustServerCertificate=True");
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            Environment.GetEnvironmentVariable("Jwt__SigningKey") ?? "dev-only-signing-key-do-not-use-in-production-min-32-chars");
        Environment.SetEnvironmentVariable("InternalService__SharedKey",
            Environment.GetEnvironmentVariable("InternalService__SharedKey") ?? TestInternalServiceKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:AutoMigrate", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<QuizDbContext>>();
            services.AddDbContext<QuizDbContext>(options =>
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

            services.RemoveAll<IOralGradingClient>();
            services.AddSingleton<IOralGradingClient, FakeOralGradingClient>();

            services.RemoveAll<IProgressReporter>();
            services.AddSingleton<IProgressReporter>(ProgressReporter);

            services.RemoveAll<IAuthQuizClient>();
            services.AddSingleton(AuthQuizClient);
            services.AddSingleton<IAuthQuizClient, FakeAuthQuizClient>();
        });
    }

    public async Task InitializeAsync()
    {
        if (!UseSqlServerBackend)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
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
