using AuthService.Api.Dtos;

namespace AuthService.Api.Services;

/// <summary>Rà soát Lần XVI (2026-08-21) — panel "Quản lý Môn học" (Admin-only, không có khái niệm
/// ownership theo Teacher như Question/Lop — chỉ Admin tạo/sửa/xóa/gán Lớp cho Môn học).</summary>
public interface IMonHocService
{
    Task<IReadOnlyList<MonHocResponse>> ListAsync(CancellationToken ct);
    Task<MonHocResponse> CreateAsync(CreateMonHocRequest request, CancellationToken ct);
    Task<MonHocResponse> UpdateAsync(Guid id, UpdateMonHocRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<MonHocResponse> AssignLopAsync(Guid id, List<Guid> lopIds, CancellationToken ct);
}
