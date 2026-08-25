using System.Text.Json;
using HealthChecks.NpgSql;
using HealthChecks.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shared.Infrastructure.HealthChecks;

public static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    /// <summary>
    /// Registers the standard health checks: an always-pass liveness check plus, if
    /// `connectionString` is provided, a Postgres readiness check tagged "ready".
    /// </summary>
    // TODO(0.2): every service is moving off Postgres onto SQL Server (see AddSharedHealthChecksSqlServer
    // below, added for auth-service's pilot migration) — once all 6 services have switched, delete this
    // overload and the Npgsql health-check package, and fold the SqlServer path into the one method.
    public static IServiceCollection AddSharedHealthChecks(this IServiceCollection services, string? connectionString)
    {
        var builder = services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.AddNpgSql(connectionString, name: "postgres", tags: [ReadyTag]);
        }

        return services;
    }

    /// <summary>
    /// SQL Server counterpart of <see cref="AddSharedHealthChecks"/>, used by services that have
    /// migrated off Postgres (auth-service first, see Part 0.1/0.2 of the DB migration).
    /// </summary>
    public static IServiceCollection AddSharedHealthChecksSqlServer(this IServiceCollection services, string? connectionString)
    {
        var builder = services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.AddSqlServer(connectionString, name: "sqlserver", tags: [ReadyTag]);
        }

        return services;
    }

    /// <summary>Maps /health (liveness — process is up) and /health/ready (readiness — dependencies reachable).</summary>
    public static WebApplication MapSharedHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponseAsync,
        });

        return app;
    }

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
