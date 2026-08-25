namespace QuizService.Api.Dtos;

/// <summary>Việc 4.2 mục 3 (2026-08-19) — dump/xóa toàn bộ dữ liệu quiz-service của 1 nhóm UserId
/// (học viên 1 Lớp) + 3 bảng Lop-Visibility theo LopId, gọi bởi admin-service khi Admin xóa toàn bộ
/// dữ liệu 1 Lớp. UserIds do admin-service tự lấy từ auth-service (roster Role=Student của đúng
/// LopId) TRƯỚC khi gọi sang đây — quiz-service không tự tra cứu Lớp/roster (không có dữ liệu đó).</summary>
public sealed record LopDataRequest(IReadOnlyList<Guid> UserIds, Guid LopId);

public sealed record QuizResultDump(Guid Id, Guid UserId, string? Chapter, decimal Score, int Correct, int Total, DateTime CreatedAtUtc);

public sealed record ExamResultDump(Guid Id, Guid UserId, decimal Score, bool IsAutoSubmitted, Guid? ExamSessionId, DateTime CreatedAtUtc);

public sealed record ExamSessionDump(Guid Id, Guid UserId, string Kind, string Status, DateTime StartedAtUtc, int ExpectedDurationSeconds, Guid? ExamResultId);

public sealed record OralResultDump(Guid Id, Guid UserId, Guid QuestionId, decimal? AiScore, Guid? ExamSessionId, DateTime CreatedAtUtc);

public sealed record WrongAnswerDump(Guid UserId, Guid QuestionId, int WrongCount, DateTime LastWrongAtUtc);

/// <summary>Dump đầy đủ dùng cho file backup tải về máy Admin TRƯỚC khi xóa (Việc 4.2 mục 3, bổ
/// sung an toàn #2 của user — "BACKUP PHẢI ĐẦY ĐỦ ĐỂ KHÔI PHỤC THỦ CÔNG"). Không có PasswordHash gì
/// ở đây (quiz-service vốn không lưu), nên không cần lọc gì thêm.</summary>
public sealed record LopDataDumpResponse(
    IReadOnlyList<QuizResultDump> QuizResults,
    IReadOnlyList<ExamResultDump> ExamResults,
    IReadOnlyList<ExamSessionDump> ExamSessions,
    IReadOnlyList<OralResultDump> OralResults,
    IReadOnlyList<WrongAnswerDump> WrongAnswers,
    IReadOnlyList<Guid> QuestionVisibilityIds,
    IReadOnlyList<Guid> EssayQuestionVisibilityIds,
    IReadOnlyList<Guid> ExamVersionVisibilityIds);

public sealed record LopDataDeleteResponse(
    int QuizResultsDeleted,
    int ExamResultsDeleted,
    int ExamSessionsDeleted,
    int OralResultsDeleted,
    int WrongAnswersDeleted,
    int QuestionVisibilityDeleted,
    int EssayQuestionVisibilityDeleted,
    int ExamVersionVisibilityDeleted);
