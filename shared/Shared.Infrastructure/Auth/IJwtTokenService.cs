namespace Shared.Infrastructure.Auth;

public sealed record TokenResult(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>Issues signed JWTs. Only auth-service should hold a concrete registration of this —
/// every other service only needs to *validate* tokens, via AddSharedJwtAuthentication.</summary>
public interface IJwtTokenService
{
    TokenResult IssueAccessToken(string userId, string email, string name, string role);
}
