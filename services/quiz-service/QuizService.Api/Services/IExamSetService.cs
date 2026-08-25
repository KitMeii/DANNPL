using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

public interface IExamSetService
{
    Task<ExamSetResponse> GenerateAsync(GenerateExamSetVersionsRequest request, Guid createdBy, string callerRole, CancellationToken ct);

    /// <summary>Việc 5 — "Bộ đề VĐ mới" từ ngân hàng câu hỏi vấn đáp có sẵn (không sinh AI mới).</summary>
    Task<ExamSetResponse> GenerateOralAsync(GenerateOralExamSetVersionsRequest request, Guid createdBy, string callerRole, CancellationToken ct);
    // Rà soát Lần XI (2026-08-21) — thêm callerUserId/callerRole vào List/GetById/Publish/Unpublish/
    // UpdateVersionLopVisibility: GV chỉ thấy/thao tác được Bộ đề do CHÍNH MÌNH tạo (Admin thấy hết)
    // — cùng lỗi đã sửa cho Question/OralQuestion/Material ở Lần VIII nhưng ExamSet/ExamVersion bị
    // bỏ sót, xác nhận qua yêu cầu người dùng kiểm tra lại toàn bộ ràng buộc backend.
    Task<IReadOnlyList<ExamSetSummaryResponse>> ListAsync(Guid callerUserId, string callerRole, CancellationToken ct);
    Task<ExamSetResponse> GetByIdAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Xuất bản CẢ MÃ ĐỀ — set IsPublishedForPractice=true cho mọi Question thuộc
    /// ExamVersion này, và ExamVersion.IsPublished=true.</summary>
    Task<int> PublishVersionAsync(Guid versionId, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Hủy xuất bản cả mã đề (thêm sau audit Việc 1, cần cho lối thoát khỏi lỗi 409 khi
    /// chặn xóa câu hỏi thuộc mã đề đã publish). Không unpublish nhầm câu vẫn hợp lệ ở mã đề khác.</summary>
    Task<int> UnpublishVersionAsync(Guid versionId, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Việc 8 — sửa lại phạm vi hiển thị của 1 mã đề đã có.</summary>
    Task<ExamVersionResponse> UpdateVersionLopVisibilityAsync(Guid versionId, List<Guid> lopIds, Guid callerUserId, string callerRole, CancellationToken ct);
}
