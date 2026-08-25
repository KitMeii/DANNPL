using AdminService.Api.Clients;

namespace AdminService.Tests.Integration;

public sealed class FakeSystemStatsClient : ISystemStatsClient
{
    public SystemOverview Overview { get; set; } = new(0, 0, 0);
    public Dictionary<Guid, ContentCounts> ContentCountsByCreator { get; } = [];
    public Dictionary<string, int> QuestionCountsByChapter { get; } = [];

    public Task<SystemOverview> GetOverviewAsync(CancellationToken ct) => Task.FromResult(Overview);

    public Task<IReadOnlyDictionary<Guid, ContentCounts>> GetContentCountsByCreatorAsync(CancellationToken ct)
    {
        IReadOnlyDictionary<Guid, ContentCounts> result = ContentCountsByCreator;
        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<string, int>> GetQuestionCountsByChapterAsync(CancellationToken ct)
    {
        IReadOnlyDictionary<string, int> result = QuestionCountsByChapter;
        return Task.FromResult(result);
    }
}
