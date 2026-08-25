namespace QuizService.Api.Dtos;

/// <summary>LopIds rỗng/null = "Toàn hệ thống" (mặc định). Có phần tử = "Chỉ Lớp cụ thể" — Việc 4.4
/// Phần A (2026-08-20), cùng quy ước với CreateQuestionRequest.</summary>
public sealed record CreateOralQuestionRequest(string? Chapter, string QuestionText, string? ExpectedAnswer, int Difficulty, List<Guid>? LopIds = null);

public sealed record UpdateOralQuestionRequest(string? Chapter, string QuestionText, string? ExpectedAnswer, int Difficulty);

/// <summary>Sửa lại phạm vi hiển thị của 1 câu hỏi vấn đáp đã có. LopIds rỗng = trả về toàn hệ thống.</summary>
public sealed record UpdateOralQuestionLopVisibilityRequest(List<Guid> LopIds);

/// <summary>Full detail including ExpectedAnswer — Teacher/Admin bank management only.</summary>
public sealed record OralQuestionResponse(Guid Id, string? Chapter, string QuestionText, string? ExpectedAnswer, int Difficulty, Guid? CreatedBy, DateTime CreatedAtUtc, IReadOnlyList<Guid> LopIds);

/// <summary>What a student sees before attempting — no ExpectedAnswer (that's only for AI grading).</summary>
public sealed record OralQuestionPracticeResponse(Guid Id, string? Chapter, string QuestionText, int Difficulty);
