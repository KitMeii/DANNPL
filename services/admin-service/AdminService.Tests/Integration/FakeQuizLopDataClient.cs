using AdminService.Api.Clients;

namespace AdminService.Tests.Integration;

/// <summary>Stands in for quiz-service's /internal/lop-data/* — seed <see cref="Dump"/> before a
/// test that needs non-empty counts. DeleteAsync just records what it was called with (Deleted*)
/// so tests can assert exact-target-only deletion; DeleteFailure lets a test simulate quiz-service
/// being unreachable mid-saga (Việc 4.2 mục 3's required "saga báo lỗi đúng bước" test).</summary>
public sealed class FakeQuizLopDataClient : IQuizLopDataClient
{
    public RemoteQuizLopDataDump Dump { get; set; } = new([], [], [], [], [], [], [], []);
    public Exception? DeleteFailure { get; set; }
    public (IReadOnlyList<Guid> UserIds, Guid LopId)? LastDeleteCall { get; private set; }
    public int DeleteCallCount { get; private set; }

    public Task<RemoteQuizLopDataDump> DumpAsync(IReadOnlyList<Guid> userIds, Guid lopId, CancellationToken ct) =>
        Task.FromResult(Dump);

    public Task<RemoteQuizLopDataDeleteResult> DeleteAsync(IReadOnlyList<Guid> userIds, Guid lopId, CancellationToken ct)
    {
        DeleteCallCount++;
        LastDeleteCall = (userIds, lopId);
        if (DeleteFailure is not null)
        {
            throw DeleteFailure;
        }

        return Task.FromResult(new RemoteQuizLopDataDeleteResult(
            Dump.QuizResults.Count, Dump.ExamResults.Count, Dump.ExamSessions.Count, Dump.OralResults.Count,
            Dump.WrongAnswers.Count, Dump.QuestionVisibilityIds.Count, Dump.EssayQuestionVisibilityIds.Count, Dump.ExamVersionVisibilityIds.Count));
    }
}
