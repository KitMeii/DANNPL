using AdminService.Api.Dtos;

namespace AdminService.Api.Services;

/// <summary>Việc 4.2 mục 3 (2026-08-19) — xóa toàn bộ dữ liệu 1 Lớp (hành động hủy diệt, Admin-only,
/// KHÔNG khôi phục). Flow 2 bước bắt buộc theo đúng thứ tự (đã duyệt): PrepareAsync (chỉ ĐỌC + dựng
/// backup, KHÔNG xóa gì) → Admin tải backup về máy → ExecuteAsync (xóa thật, saga 3 service: quiz →
/// progress → auth). PrepareAsync thất bại = KHÔNG có PreparationId nào phát sinh = ExecuteAsync
/// không thể gọi được — "backup thất bại thì không có gì để xóa" được đảm bảo bằng chính luồng dữ
/// liệu, không phải quy ước.</summary>
public interface ILopDeletionService
{
    Task<PrepareLopDeletionResponse> PrepareAsync(Guid lopId, Guid adminUserId, CancellationToken ct);

    /// <summary>Xác thực LẠI ConfirmedLopTen so với tên Lớp tại thời điểm Prepare (không tin FE),
    /// PreparationId phải tồn tại/khớp lopId/còn hiệu lực (30 phút)/chưa Completed. Saga dừng ở bước
    /// đầu tiên thất bại — báo cáo chính xác bước nào xong/bước nào lỗi, an toàn gọi lại (mọi thao
    /// tác xóa ở cả 3 service đều idempotent).</summary>
    Task<ExecuteLopDeletionResponse> ExecuteAsync(Guid lopId, ExecuteLopDeletionRequest request, Guid adminUserId, CancellationToken ct);

    Task<IReadOnlyList<LopDeletionAuditResponse>> GetAuditHistoryAsync(int top, CancellationToken ct);
}
