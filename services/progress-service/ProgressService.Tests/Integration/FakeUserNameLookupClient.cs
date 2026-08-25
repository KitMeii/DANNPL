using ProgressService.Api.Clients;

namespace ProgressService.Tests.Integration;

/// <summary>Stands in for auth-service (called for leaderboard name+role enrichment) so tests
/// don't need a live auth-service instance.</summary>
public sealed class FakeUserNameLookupClient : IUserNameLookupClient
{
    public Dictionary<Guid, UserName> Names { get; } = new();

    /// <summary>Convenience seed helper — mặc định Role="Student" vì phần lớn test không quan tâm
    /// role, chỉ Việc B's leaderboard-role-filter tests mới cần seed Teacher/Admin tường minh.</summary>
    public void Add(Guid id, string name, string role = "Student") => Names[id] = new UserName(id, name, role);

    public Task<IReadOnlyDictionary<Guid, UserName>> GetNamesAsync(IReadOnlyList<Guid> userIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, UserName>>(
            userIds.Where(Names.ContainsKey).ToDictionary(id => id, id => Names[id]));
}
