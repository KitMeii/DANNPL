using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

/// <summary>Việc 4.2 mục 3 — xem remarks ở LopDataAdminDtos.cs.</summary>
public interface ILopDataAdminService
{
    Task<LopDataDumpResponse> DumpAsync(LopDataRequest request, CancellationToken ct);

    /// <summary>Idempotent — xóa 0 dòng khớp không phải lỗi (dùng ExecuteDeleteAsync), an toàn
    /// gọi lại sau 1 lần chạy trước đó thất bại giữa chừng ở bước khác trong saga.</summary>
    Task<LopDataDeleteResponse> DeleteAsync(LopDataRequest request, CancellationToken ct);
}
