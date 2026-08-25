namespace ProgressService.Api.Clients;

public sealed record UserName(Guid Id, string Name, string Role);

/// <summary>progress-service doesn't own user profile data — it calls auth-service's
/// GET /api/v1/auth/users/names to enrich a leaderboard with display names, rather than joining
/// across service boundaries. Role included (Việc B, 2026-08-16) so the leaderboard can exclude
/// Teacher/Admin accounts — progress-service has no other way to know a user's role.</summary>
public interface IUserNameLookupClient
{
    Task<IReadOnlyDictionary<Guid, UserName>> GetNamesAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);
}
