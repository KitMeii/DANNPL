using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Infrastructure.Auth;

public static class InternalServiceAuthExtensions
{
    /// <summary>Binds the "InternalService" config section, backing <see cref="RequireInternalServiceKeyFilter"/>.
    /// Call from any service that either exposes or calls a service-to-service-only endpoint.</summary>
    public static IServiceCollection AddInternalServiceAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InternalServiceAuthOptions>(configuration.GetSection(InternalServiceAuthOptions.SectionName));
        return services;
    }
}
