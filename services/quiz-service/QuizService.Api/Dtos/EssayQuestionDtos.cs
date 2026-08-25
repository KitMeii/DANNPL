namespace QuizService.Api.Dtos;

/// <summary>LopIds rỗng/null = "Toàn hệ thống" (mặc định, hành vi cũ) — Việc 8 (2026-08-16).</summary>
public sealed record CreateEssayQuestionRequest(string? Chapter, string QuestionText, string? SuggestedAnswer, string SourceType = "Manual", Guid? SourceMaterialId = null, List<Guid>? LopIds = null);

public sealed record UpdateEssayQuestionRequest(string? Chapter, string QuestionText, string? SuggestedAnswer);

/// <summary>Việc 8 — sửa lại phạm vi hiển thị của 1 câu tự luận đã có. LopIds rỗng = trả về toàn hệ
/// thống.</summary>
public sealed record UpdateEssayQuestionLopVisibilityRequest(List<Guid> LopIds);

/// <summary>Full detail including SuggestedAnswer — Teacher/Admin bank management only.</summary>
public sealed record EssayQuestionResponse(Guid Id, string? Chapter, string QuestionText, string? SuggestedAnswer, Guid? CreatedBy, DateTime CreatedAtUtc, string SourceType, Guid? SourceMaterialId, bool IsPublishedForPractice, IReadOnlyList<Guid> LopIds);

/// <summary>What a student sees — no SuggestedAnswer (teacher grades manually).</summary>
public sealed record EssayQuestionPracticeResponse(Guid Id, string? Chapter, string QuestionText);
