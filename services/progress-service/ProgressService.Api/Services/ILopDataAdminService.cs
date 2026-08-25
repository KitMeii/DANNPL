using ProgressService.Api.Dtos;

namespace ProgressService.Api.Services;

/// <summary>Việc 4.2 mục 3 — xem remarks ở LopDataAdminDtos.cs.</summary>
public interface ILopDataAdminService
{
    Task<ProgressLopDataDumpResponse> DumpAsync(ProgressLopDataRequest request, CancellationToken ct);

    /// <summary>Idempotent — ExecuteDeleteAsync, xóa 0 dòng khớp không phải lỗi.</summary>
    Task<ProgressLopDataDeleteResponse> DeleteAsync(ProgressLopDataRequest request, CancellationToken ct);
}
