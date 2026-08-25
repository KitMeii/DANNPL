namespace AdminService.Api.Entities;

public sealed class SystemConfig
{
    public required string Key { get; init; }
    public required string Value { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
