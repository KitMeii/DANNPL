namespace QuizService.Api.Dtos;

/// <summary>LopIds rỗng/null = "Toàn hệ thống" (mặc định, hành vi cũ). Có phần tử = "Chỉ Lớp cụ
/// thể" — Việc 8 (2026-08-16).</summary>
public sealed record CreateQuestionRequest(string? Chapter, string QuestionText, string OptionA, string OptionB, string OptionC, string OptionD, int CorrectAnswer, string? Explanation, string SourceType = "Manual", Guid? SourceMaterialId = null, int? Difficulty = null, string? Topic = null, List<Guid>? LopIds = null);

public sealed record UpdateQuestionRequest(string? Chapter, string QuestionText, string OptionA, string OptionB, string OptionC, string OptionD, int CorrectAnswer, string? Explanation);

/// <summary>Việc 8 — sửa lại phạm vi hiển thị của 1 câu hỏi đã có (kể cả câu tạo trước Việc 8, vốn
/// mặc định toàn hệ thống). LopIds rỗng = trả về toàn hệ thống.</summary>
public sealed record UpdateQuestionLopVisibilityRequest(List<Guid> LopIds);

/// <summary>Full question detail, including the answer key — Teacher/Admin bank management only.</summary>
public sealed record QuestionResponse(Guid Id, string? Chapter, string QuestionText, string OptionA, string OptionB, string OptionC, string OptionD, int CorrectAnswer, string? Explanation, Guid? CreatedBy, DateTime CreatedAtUtc, string SourceType, Guid? SourceMaterialId, int? Difficulty, string? Topic, bool IsPublished, IReadOnlyList<Guid> LopIds);

/// <summary>What a student sees while attempting a quiz — no CorrectAnswer, no Explanation.</summary>
public sealed record QuizQuestionResponse(Guid Id, string? Chapter, string QuestionText, string OptionA, string OptionB, string OptionC, string OptionD);
