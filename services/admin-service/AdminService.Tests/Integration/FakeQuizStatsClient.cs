using AdminService.Api.Clients;

namespace AdminService.Tests.Integration;

/// <summary>Stands in for quiz-service's batch score endpoint. Seed <see cref="Scores"/> before
/// each test — a userId with no entry returns a summary with everything null/0, matching what the
/// real endpoint does for a user with zero attempts.</summary>
public sealed class FakeQuizStatsClient : IQuizStatsClient
{
    public Dictionary<Guid, RemoteUserScore> Scores { get; } = [];

    public Task<IReadOnlyList<RemoteUserScore>> GetScoresByUsersAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        IReadOnlyList<RemoteUserScore> result = userIds
            .Select(id => Scores.TryGetValue(id, out var score) ? score : new RemoteUserScore(id, null, 0, null, 0))
            .ToList();
        return Task.FromResult(result);
    }
}
