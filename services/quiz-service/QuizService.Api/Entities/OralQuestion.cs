namespace QuizService.Api.Entities;

public sealed class OralQuestion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Chapter { get; set; }
    public required string QuestionText { get; set; }
    public string? ExpectedAnswer { get; set; }
    public int Difficulty { get; set; } = 1;
    public Guid? CreatedBy { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
