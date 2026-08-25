namespace AdminService.Api.Clients;

public sealed record RemoteUser(
    Guid Id, string Email, string Name, string Role, Guid? LopId = null, string ChucVu = "Học viên",
    string? AvatarUrl = null, string? CapBac = null, string? SoDienThoai = null, string? NamHoc = null, string? BoMonKhoa = null,
    string? ChucVuGV = null, string? MonHocPhuTrach = null, bool IsLocked = false);

public sealed record RemoteKhoa(Guid Id, string Ten);

public sealed record RemoteLop(Guid Id, string Ten, Guid KhoaId, Guid? GiaoVienId);

/// <summary>admin-service owns no user/khóa/lớp data itself — every read/write goes to
/// auth-service, which owns Users/Khoa/Lop. See AuthService.Api/Endpoints/AuthEndpoints.cs and
/// KhoaLopEndpoints.cs for the admin-only endpoints this calls.</summary>
public interface IAuthAdminClient
{
    Task<IReadOnlyList<RemoteUser>> ListUsersAsync(string? role, Guid? lopId, Guid? khoaId, CancellationToken ct);
    Task<RemoteUser> ChangeRoleAsync(Guid userId, string newRole, CancellationToken ct);

    /// <summary>Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp (Role bất kỳ).</summary>
    Task<RemoteUser> CreateUserAsync(string email, string password, string name, string role, CancellationToken ct);

    /// <summary>Rà soát Lần XII (2026-08-21) — khóa/mở khóa tài khoản (chặn đăng nhập, giữ nguyên
    /// dữ liệu).</summary>
    Task<RemoteUser> SetUserLockedAsync(Guid userId, bool isLocked, CancellationToken ct);
    Task<RemoteKhoa> GetKhoaAsync(Guid khoaId, CancellationToken ct);
    Task<RemoteLop> GetLopAsync(Guid lopId, CancellationToken ct);

    /// <summary>Việc 7 (2026-08-16) — toàn bộ Lớp (mọi Khóa) kèm GiaoVienId, dùng để nhóm theo
    /// giáo viên cho Dashboard "Theo dõi Giáo viên". Gọi GET /api/v1/auth/lop (Admin-only, đã có
    /// sẵn từ trước) không truyền khoaId — auth-service coi thiếu tham số là "tất cả".</summary>
    Task<IReadOnlyList<RemoteLop>> ListLopAsync(CancellationToken ct);

    /// <summary>Gap 2: roster của 1 Lớp qua GET /api/v1/auth/lop/{id}/hoc-vien — KHÔNG dùng
    /// ListUsersAsync ở đây vì endpoint đó (GET /api/v1/auth/users) là Admin-only ở chính
    /// auth-service (RequireRole(Admin) khai báo tĩnh), sẽ 403 khi forward JWT của Teacher dù
    /// LopKhoaStatsService đã tự kiểm tra "đúng GV chủ nhiệm" trước đó. Endpoint hoc-vien này tự
    /// cho phép Teacher (kèm kiểm tra data-dependent riêng ở auth-service), nên JWT Teacher forward
    /// qua vẫn được chấp nhận hợp lệ.</summary>
    Task<IReadOnlyList<RemoteUser>> ListHocVienAsync(Guid lopId, CancellationToken ct);

    /// <summary>Việc 4.2 mục 3 (2026-08-19) — toàn bộ nhật ký hoạt động của 1 Lớp (không cap), dùng
    /// để dựng backup TRƯỚC khi xóa. Khác GET /lop/{id}/activity-log (UI, cap 200 dòng).</summary>
    Task<IReadOnlyList<RemoteLopActivityLog>> ListAllLopActivityLogAsync(Guid lopId, CancellationToken ct);

    /// <summary>Việc 4.2 mục 3 — bước CUỐI trong saga xóa toàn bộ dữ liệu Lớp. Idempotent — xem
    /// remarks DeleteAllLopDataResponse ở auth-service.</summary>
    Task<RemoteDeleteAllLopDataResult> DeleteAllLopDataAsync(Guid lopId, CancellationToken ct);
}

public sealed record RemoteLopActivityLog(
    Guid Id, Guid LopId, Guid ActorUserId, string ActorName, string ActionType,
    Guid TargetUserId, string TargetName, string? OldValue, string? NewValue, DateTime CreatedAtUtc);

public sealed record RemoteDeleteAllLopDataResult(int UsersDeleted, int ActivityLogsDeleted, bool LopDeleted);
