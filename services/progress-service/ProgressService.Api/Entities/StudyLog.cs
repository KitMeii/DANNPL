namespace ProgressService.Api.Entities;

public sealed class StudyLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid UserId { get; init; }
    public required DateOnly StudyDate { get; init; }
    public int Minutes { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
