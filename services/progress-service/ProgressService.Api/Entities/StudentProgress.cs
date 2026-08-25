namespace ProgressService.Api.Entities;

public sealed class StudentProgress
{
    public required Guid UserId { get; init; }
    public int Streak { get; set; }
    public DateOnly? LastStudyDate { get; set; }
    public int TotalStudyMinutes { get; set; }

    /// <summary>Backing fields for a running average — AvgScore is always (ScoreSum / TotalAttempts),
    /// computed on read rather than stored, so it can never drift out of sync.</summary>
    public int TotalAttempts { get; set; }
    public decimal ScoreSum { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
