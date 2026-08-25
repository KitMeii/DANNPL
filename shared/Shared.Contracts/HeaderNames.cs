namespace Shared.Contracts;

/// <summary>HTTP header names shared between the gateway and downstream services.</summary>
public static class HeaderNames
{
    /// <summary>Correlation id generated/forwarded by the gateway, propagated through every downstream call and log line.</summary>
    public const string CorrelationId = "X-Correlation-Id";

    /// <summary>User id (JWT sub claim) forwarded by the gateway once it has parsed the caller's JWT —
    /// informational/logging use only. No service currently reads this header for an authorization
    /// decision; every service independently re-validates the JWT itself via
    /// AddSharedJwtAuthentication + [Authorize], which is the actual authorization mechanism. Do not
    /// start trusting this header for authz without also closing the gateway's catch-all proxy routes
    /// (see the Phần A RBAC audit note in docker-compose.yml on why "only reachable through the
    /// gateway" is not by itself a sufficient trust boundary for every route).</summary>
    public const string UserId = "X-User-Id";

    /// <summary>Role claim forwarded by the gateway — see remarks on <see cref="UserId"/>.</summary>
    public const string UserRole = "X-User-Role";
}
