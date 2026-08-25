namespace AdminService.Api.Entities;

public sealed class RoleChangeAudit
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid AdminUserId { get; init; }
    public required Guid TargetUserId { get; init; }
    public required string OldRole { get; init; }
    public required string NewRole { get; init; }
    public DateTime ChangedAtUtc { get; init; } = DateTime.UtcNow;
}
