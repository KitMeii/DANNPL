using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Shared.Infrastructure.Auth;

/// <summary>
/// Endpoint filter for admin-facing endpoints that are only meant to be called service-to-service
/// (e.g. auth-service's GET /users and PUT /users/{id}/role, whose only intended caller is
/// admin-service — see the remarks on AdminService.Api's HttpAuthAdminClient). An
/// [Authorize(Roles=Admin)] JWT check alone is not enough here: the gateway proxies every
/// sub-path of a service's route prefix (see gateway/appsettings.json ReverseProxy:Routes), so
/// any client holding a valid Admin JWT could otherwise call these endpoints directly and bypass
/// admin-service's mandatory RoleChangeAudit log. This filter requires the caller to also send
/// the X-Internal-Key header matching InternalService:SharedKey, a secret only admin-service
/// knows, closing that bypass while still keeping the [Authorize(Roles=Admin)] check as
/// defense-in-depth.
/// </summary>
public sealed class RequireInternalServiceKeyFilter(IOptions<InternalServiceAuthOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-Internal-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = options.Value.SharedKey;
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(expected) || provided != expected)
        {
            throw new UnauthorizedAccessException("Endpoint này chỉ dành cho gọi nội bộ giữa các service.");
        }

        return await next(context);
    }
}
