namespace QuizService.Api.Entities;

/// <summary>Song song với <see cref="QuestionLopVisibility"/> nhưng cho câu hỏi tự luận — xem
/// summary ở đó cho cơ chế đầy đủ. Việc 8 (2026-08-16).</summary>
public sealed class EssayQuestionLopVisibility
{
    public required Guid EssayQuestionId { get; init; }
    public required Guid LopId { get; init; }
}
