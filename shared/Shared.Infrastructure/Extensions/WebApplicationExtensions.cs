using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Shared.Infrastructure.Middleware;

namespace Shared.Infrastructure.Extensions;

public static class WebApplicationExtensions
{
    public const string FrontendCorsPolicy = "FrontendCors";

    /// <summary>
    /// Registers a CORS policy that allows only the origins listed under config key
    /// "Cors:AllowedOrigins" (comma-separated) — never AllowAnyOrigin.
    /// </summary>
    public static IServiceCollection AddSharedCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Wires the standard middleware pipeline in the required order: correlation-id (so it's
    /// available to everything downstream, including exception logging) → exception handling →
    /// Serilog request logging → CORS.
    /// </summary>
    public static WebApplication UseSharedMiddleware(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseCors(FrontendCorsPolicy);

        return app;
    }
}
