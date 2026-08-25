using AdminService.Api.Dtos;

namespace AdminService.Api.Services;

public interface IUserManagementService
{
    /// <summary>lopId/khoaId lọc thêm theo Lớp/Khóa (Bước D) — kết hợp AND với role nếu cả 2
    /// cùng truyền, giống hệt auth-service's ListUsersAsync mà đây forward tới.</summary>
    Task<IReadOnlyList<UserSummaryResponse>> ListUsersAsync(string? role, Guid? lopId, Guid? khoaId, CancellationToken ct);
    Task<UserSummaryResponse> ChangeRoleAsync(Guid adminUserId, Guid targetUserId, string newRole, CancellationToken ct);
    Task<IReadOnlyList<RoleChangeAuditResponse>> GetAuditLogAsync(int top, CancellationToken ct);

    /// <summary>Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp (Role bất kỳ).</summary>
    Task<UserSummaryResponse> CreateUserAsync(string email, string password, string name, string role, CancellationToken ct);

    /// <summary>Rà soát Lần XII (2026-08-21) — khóa/mở khóa tài khoản.</summary>
    Task<UserSummaryResponse> SetUserLockedAsync(Guid targetUserId, bool isLocked, CancellationToken ct);
}
