using System.Security.Claims;

namespace Shared.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the JWT sub claim as the caller's user id. Throws if absent — call only
    /// behind [Authorize]/RequireAuthorization, where a missing sub means a malformed token
    /// that should never have passed validation.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return Guid.Parse(value ?? throw new InvalidOperationException("JWT is missing the sub claim."));
    }

    public static string GetRole(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role) ?? throw new InvalidOperationException("JWT is missing the role claim.");
}
