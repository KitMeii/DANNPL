using ContentService.Api.Dtos;

namespace ContentService.Api.Services;

public interface IMaterialService
{
    /// <summary>Rà soát Lần VIII (2026-08-21) — callerUserId/callerRole: Teacher chỉ thấy tài liệu
    /// chính mình tải lên; Student/Admin không lọc (xem remarks ở MaterialService).</summary>
    Task<IReadOnlyList<MaterialResponse>> ListAsync(bool includeInactive, string? chapter, Guid callerUserId, string callerRole, CancellationToken ct);
    Task<MaterialResponse> GetByIdAsync(Guid id, bool includeInactive, CancellationToken ct);
    Task<MaterialResponse> CreateAsync(CreateMaterialRequest request, Guid uploadedBy, CancellationToken ct);

    /// <summary>Rà soát Lần VIII — chỉ người tải lên hoặc Admin được sửa/xóa.</summary>
    Task<MaterialResponse> UpdateAsync(Guid id, UpdateMaterialRequest request, Guid callerUserId, string callerRole, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct);
    Task<int> IncrementViewCountAsync(Guid id, CancellationToken ct);
}
