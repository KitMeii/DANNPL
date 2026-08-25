namespace AuthService.Api.Entities;

/// <summary>Nhật ký hoạt động cấp Lớp (Gap 2 mục 3) — KHÁC với AdminService's RoleChangeAudit
/// (đổi Role hệ thống Student/Teacher/Admin, không liên quan Lớp). Ghi mỗi khi ChucVu học viên đổi
/// hoặc học viên được thêm/gỡ khỏi Lớp, do Admin HOẶC GV chủ nhiệm thao tác. Không đặt FK ràng buộc
/// tới Lop/Users — đây là nhật ký lịch sử, phải sống sót được kể cả khi Lop rỗng bị xóa sau đó
/// (cùng lý do admin.role_change_audits không có FK tới auth.users).</summary>
public sealed class LopActivityLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid LopId { get; init; }
    public required Guid ActorUserId { get; init; }
    public required string ActionType { get; init; }
    public required Guid TargetUserId { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

public static class LopActivityActionTypes
{
    public const string ChucVuChanged = "ChucVuChanged";
    public const string StudentAdded = "StudentAdded";
    public const string StudentRemoved = "StudentRemoved";
}
