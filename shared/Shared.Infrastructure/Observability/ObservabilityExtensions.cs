using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Shared.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Wires Serilog console + Seq sinks. Seq URL comes from config key "Seq:Url" (env var
    /// Seq__Url); if unset, Seq sink is skipped so a solo `dotnet run` in dev still works without
    /// a running Seq container. Every log line is enriched with the service name and, per-request,
    /// the CorrelationId pushed by CorrelationIdMiddleware.
    /// </summary>
    public static WebApplicationBuilder AddSharedObservability(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .Enrich.WithMachineName()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] ({Service}) {CorrelationId} {Message:lj}{NewLine}{Exception}");

            var seqUrl = context.Configuration["Seq:Url"];
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                loggerConfig.WriteTo.Seq(seqUrl);
            }
        });

        return builder;
    }
}
