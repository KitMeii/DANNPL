namespace AdminService.Api.Clients;

public sealed record RemoteQuizResult(Guid Id, Guid UserId, string? Chapter, decimal Score, int Correct, int Total, DateTime CreatedAtUtc);
public sealed record RemoteExamResult(Guid Id, Guid UserId, decimal Score, bool IsAutoSubmitted, Guid? ExamSessionId, DateTime CreatedAtUtc);
public sealed record RemoteExamSession(Guid Id, Guid UserId, string Kind, string Status, DateTime StartedAtUtc, int ExpectedDurationSeconds, Guid? ExamResultId);
public sealed record RemoteOralResult(Guid Id, Guid UserId, Guid QuestionId, decimal? AiScore, Guid? ExamSessionId, DateTime CreatedAtUtc);
public sealed record RemoteWrongAnswer(Guid UserId, Guid QuestionId, int WrongCount, DateTime LastWrongAtUtc);

public sealed record RemoteQuizLopDataDump(
    IReadOnlyList<RemoteQuizResult> QuizResults,
    IReadOnlyList<RemoteExamResult> ExamResults,
    IReadOnlyList<RemoteExamSession> ExamSessions,
    IReadOnlyList<RemoteOralResult> OralResults,
    IReadOnlyList<RemoteWrongAnswer> WrongAnswers,
    IReadOnlyList<Guid> QuestionVisibilityIds,
    IReadOnlyList<Guid> EssayQuestionVisibilityIds,
    IReadOnlyList<Guid> ExamVersionVisibilityIds);

public sealed record RemoteQuizLopDataDeleteResult(
    int QuizResultsDeleted, int ExamResultsDeleted, int ExamSessionsDeleted, int OralResultsDeleted,
    int WrongAnswersDeleted, int QuestionVisibilityDeleted, int EssayQuestionVisibilityDeleted, int ExamVersionVisibilityDeleted);

/// <summary>Việc 4.2 mục 3 (2026-08-19) — dump/xóa dữ liệu quiz-service của 1 Lớp (kết quả TN/thi/
/// vấn đáp + 3 bảng phạm vi hiển thị). Gọi tới quiz-service's /internal/lop-data/* (RequireInternalServiceKeyFilter).</summary>
public interface IQuizLopDataClient
{
    Task<RemoteQuizLopDataDump> DumpAsync(IReadOnlyList<Guid> userIds, Guid lopId, CancellationToken ct);
    Task<RemoteQuizLopDataDeleteResult> DeleteAsync(IReadOnlyList<Guid> userIds, Guid lopId, CancellationToken ct);
}
