namespace QuizService.Api.Dtos;

public sealed record SubmitAnswerItem(Guid QuestionId, int SelectedOption);

public sealed record SubmitQuizRequest(string? Chapter, List<SubmitAnswerItem> Answers);

// Việc 4.1 (2026-08-19) — SessionId tùy chọn: null giữ nguyên hành vi cũ (nộp không qua session,
// vẫn hoạt động — tương thích ngược). Có giá trị thì gắn kết quả vào đúng phiên, tránh lazy-check
// sau này chốt trùng (xem QuizAttemptService.SubmitExamAsync).
public sealed record SubmitExamRequest(List<SubmitAnswerItem> Answers, int TimeSpentSeconds, Guid? SessionId = null);

/// <summary>Per-question grading detail returned only AFTER submission — this is the one place
/// CorrectAnswer is allowed to reach the client, since by then the student has already answered.</summary>
public sealed record GradedAnswer(Guid QuestionId, int SelectedOption, int CorrectAnswer, bool IsCorrect, string? Explanation);

public sealed record SubmitResultResponse(decimal Score, int Correct, int Total, IReadOnlyList<GradedAnswer> Details);

public sealed record WrongAnswerResponse(Guid QuestionId, string QuestionText, string? Chapter, int WrongCount, DateTime LastWrongAtUtc);

/// <summary>One row of a student's own history — practice or exam, distinguished by Kind.
/// Feeds tien-do.html's per-chapter breakdown and progress-service's leaderboard aggregate.
/// IsAutoSubmitted (Việc 4.1) — always false for "practice" (QuizResult never has this concept);
/// only "exam" rows can be true, meaning chống-thoát finalized it, not a manual Nộp.</summary>
public sealed record MyResultResponse(Guid Id, string Kind, string? Chapter, decimal Score, int Correct, int Total, DateTime CreatedAtUtc, bool IsAutoSubmitted = false);

public sealed record SubmitOralRequest(Guid QuestionId, string MainAnswer, List<string>? FollowupAnswers, Guid? SessionId = null);

public sealed record OralResultResponse(Guid Id, Guid QuestionId, string MainAnswer, decimal AiScore, string? AiComment, IReadOnlyDictionary<string, decimal>? RubricScores, DateTime CreatedAtUtc);

// ===================== Việc 4.1 (2026-08-19) — Chống thoát thi thử =====================

/// <summary>Client báo cáo CHÍNH XÁC bộ câu hỏi vừa được giao (đã fetch để hiển thị) — server lưu
/// làm "mẫu số thật" cho trường hợp phải tự động nộp do bỏ dở. Xem ExamSession.QuestionIdsJson
/// remarks về vì sao tin tưởng client ở đúng field này là chấp nhận được.</summary>
public sealed record StartExamSessionRequest(List<Guid> QuestionIds, int ExpectedDurationSeconds);

public sealed record StartExamSessionResponse(Guid SessionId);

/// <summary>Lớp 1 (beacon lúc rời trang) gọi endpoint này với bất kỳ câu nào đã kịp trả lời trong
/// bộ nhớ JS tại thời điểm thoát. Lớp 2 (lazy-check) tự gọi phần lõi tương ứng phía server với
/// answers rỗng (không có gì để lấy — beacon không tới nơi). Cả 2 đường đều idempotent, xem
/// QuizAttemptService.AutoSubmitAbandonedSessionAsync.</summary>
public sealed record AutoSubmitExamRequest(Guid SessionId, List<SubmitAnswerItem> Answers);

/// <summary>Vấn đáp "chốt phiên bỏ dở" không cần gửi câu trả lời — mỗi câu đã được lưu ngay khi trả
/// lời (POST /oral/submit), không có gì để mất; chỉ cần đánh dấu phiên là bị bỏ dở.</summary>
public sealed record AbandonOralSessionRequest(Guid SessionId);

public sealed record OralSessionResponse(Guid Id, string Status, DateTime StartedAtUtc, int QuestionCount, int AnsweredCount);
