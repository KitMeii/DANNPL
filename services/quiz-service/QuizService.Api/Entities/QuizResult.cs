namespace QuizService.Api.Entities;

public sealed class QuizResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid UserId { get; init; }
    public string? Chapter { get; set; }
    public decimal Score { get; set; }
    public int Correct { get; set; }
    public int Total { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
