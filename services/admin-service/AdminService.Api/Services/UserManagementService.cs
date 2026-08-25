using AdminService.Api.Clients;
using AdminService.Api.Data;
using AdminService.Api.Dtos;
using AdminService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Common;

namespace AdminService.Api.Services;

public sealed class UserManagementService(IAuthAdminClient authClient, AdminDbContext db) : IUserManagementService
{
    public async Task<IReadOnlyList<UserSummaryResponse>> ListUsersAsync(string? role, Guid? lopId, Guid? khoaId, CancellationToken ct)
    {
        var users = await authClient.ListUsersAsync(role, lopId, khoaId, ct);
        return users.Select(u => new UserSummaryResponse(u.Id, u.Email, u.Name, u.Role, u.LopId, u.ChucVu, u.CapBac, u.MonHocPhuTrach, u.IsLocked, u.ChucVuGV, u.NamHoc)).ToList();
    }

    public async Task<UserSummaryResponse> CreateUserAsync(string email, string password, string name, string role, CancellationToken ct)
    {
        var created = await authClient.CreateUserAsync(email, password, name, role, ct);
        return new UserSummaryResponse(created.Id, created.Email, created.Name, created.Role, IsLocked: created.IsLocked, ChucVuGV: created.ChucVuGV, NamHoc: created.NamHoc);
    }

    public async Task<UserSummaryResponse> SetUserLockedAsync(Guid targetUserId, bool isLocked, CancellationToken ct)
    {
        var updated = await authClient.SetUserLockedAsync(targetUserId, isLocked, ct);
        return new UserSummaryResponse(updated.Id, updated.Email, updated.Name, updated.Role, IsLocked: updated.IsLocked, ChucVuGV: updated.ChucVuGV, NamHoc: updated.NamHoc);
    }

    public async Task<UserSummaryResponse> ChangeRoleAsync(Guid adminUserId, Guid targetUserId, string newRole, CancellationToken ct)
    {
        var allUsers = await authClient.ListUsersAsync(null, null, null, ct);
        var target = allUsers.FirstOrDefault(u => u.Id == targetUserId)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        var updated = await authClient.ChangeRoleAsync(targetUserId, newRole, ct);

        db.RoleChangeAudits.Add(new RoleChangeAudit
        {
            AdminUserId = adminUserId,
            TargetUserId = targetUserId,
            OldRole = target.Role,
            NewRole = newRole,
        });
        await db.SaveChangesAsync(ct);

        return new UserSummaryResponse(updated.Id, updated.Email, updated.Name, updated.Role, IsLocked: updated.IsLocked, ChucVuGV: updated.ChucVuGV, NamHoc: updated.NamHoc);
    }

    public async Task<IReadOnlyList<RoleChangeAuditResponse>> GetAuditLogAsync(int top, CancellationToken ct)
    {
        var entries = await db.RoleChangeAudits
            .OrderByDescending(a => a.ChangedAtUtc)
            .Take(top)
            .ToListAsync(ct);

        // Rà soát Lần XVII (2026-08-21) — resolve tên GV/Admin đã tra ở đây (không gọi thêm lần
        // nào khác) — 1 lần ListUsersAsync(null,null,null) đủ cho mọi dòng, cùng cách ChangeRoleAsync
        // đã làm để validate target tồn tại trước khi đổi role.
        var allUsers = await authClient.ListUsersAsync(null, null, null, ct);
        var nameById = allUsers.ToDictionary(u => u.Id, u => u.Name);

        return entries
            .Select(a => new RoleChangeAuditResponse(
                a.Id, a.AdminUserId, nameById.GetValueOrDefault(a.AdminUserId),
                a.TargetUserId, nameById.GetValueOrDefault(a.TargetUserId),
                a.OldRole, a.NewRole, a.ChangedAtUtc))
            .ToList();
    }
}
