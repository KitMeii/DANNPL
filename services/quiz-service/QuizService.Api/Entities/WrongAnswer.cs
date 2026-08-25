namespace QuizService.Api.Entities;

public sealed class WrongAnswer
{
    public required Guid UserId { get; init; }
    public required Guid QuestionId { get; init; }
    public int WrongCount { get; set; }
    public DateTime LastWrongAtUtc { get; set; } = DateTime.UtcNow;
}
