namespace ProgressService.Api.Dtos;

/// <summary>Việc 4.2 mục 3 (2026-08-19) — song song với quiz-service's LopDataAdminDtos.cs, xem
/// remarks ở đó. progress-service không có khái niệm LopId (StudentProgress/StudyLog chỉ khóa theo
/// UserId), nên request ở đây chỉ cần UserIds.</summary>
public sealed record ProgressLopDataRequest(IReadOnlyList<Guid> UserIds);

public sealed record StudentProgressDump(Guid UserId, int Streak, DateOnly? LastStudyDate, int TotalStudyMinutes, int TotalAttempts, decimal ScoreSum, DateTime UpdatedAtUtc);

public sealed record StudyLogDump(Guid Id, Guid UserId, DateOnly StudyDate, int Minutes, DateTime CreatedAtUtc);

public sealed record ProgressLopDataDumpResponse(
    IReadOnlyList<StudentProgressDump> StudentProgress,
    IReadOnlyList<StudyLogDump> StudyLogs);

public sealed record ProgressLopDataDeleteResponse(int StudentProgressDeleted, int StudyLogsDeleted);
