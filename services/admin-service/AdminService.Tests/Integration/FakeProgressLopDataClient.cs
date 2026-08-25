using AdminService.Api.Clients;

namespace AdminService.Tests.Integration;

/// <summary>Stands in for progress-service's /internal/lop-data/* — xem remarks FakeQuizLopDataClient.</summary>
public sealed class FakeProgressLopDataClient : IProgressLopDataClient
{
    public RemoteProgressLopDataDump Dump { get; set; } = new([], []);
    public Exception? DeleteFailure { get; set; }
    public IReadOnlyList<Guid>? LastDeleteCall { get; private set; }
    public int DeleteCallCount { get; private set; }

    public Task<RemoteProgressLopDataDump> DumpAsync(IReadOnlyList<Guid> userIds, CancellationToken ct) =>
        Task.FromResult(Dump);

    public Task<RemoteProgressLopDataDeleteResult> DeleteAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        DeleteCallCount++;
        LastDeleteCall = userIds;
        if (DeleteFailure is not null)
        {
            throw DeleteFailure;
        }

        return Task.FromResult(new RemoteProgressLopDataDeleteResult(Dump.StudentProgress.Count, Dump.StudyLogs.Count));
    }
}
