using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

public interface IQuestionService
{
    /// <summary>Rà soát Lần VIII (2026-08-21) — Ngân hàng câu hỏi TRƯỚC dùng chung mọi GV (xác nhận
    /// là lỗi thật qua audit theo yêu cầu người dùng). Giờ: Teacher chỉ thấy câu CHÍNH MÌNH tạo
    /// (CreatedBy == callerUserId), Admin thấy toàn bộ (vai trò giám sát hệ thống, không đổi).</summary>
    Task<IReadOnlyList<QuestionResponse>> ListAsync(string? chapter, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>callerLopId = Lớp của người gọi (null nếu chưa gán Lớp) — Việc 8: câu hỏi có phạm
    /// vi giới hạn chỉ hiện nếu khớp đúng Lớp này; câu hỏi toàn hệ thống luôn hiện. KHÔNG lọc theo
    /// người tạo — dùng để kiểm tra "câu nào đang xuất bản, học viên lớp X thấy được" (test lop-
    /// visibility), độc lập với ListAsync (GV quản lý ngân hàng của mình).</summary>
    Task<IReadOnlyList<QuizQuestionResponse>> ListPublishedAsync(string? chapter, Guid? callerLopId, CancellationToken ct);

    Task<QuestionResponse> CreateAsync(CreateQuestionRequest request, Guid createdBy, string callerRole, CancellationToken ct);

    /// <summary>Rà soát Lần VIII — chỉ người tạo câu hỏi hoặc Admin được sửa/xóa/xuất bản/đổi phạm
    /// vi Lớp — trước đây bất kỳ Teacher/Admin nào cũng sửa/xóa được câu của GV khác.</summary>
    Task<QuestionResponse> UpdateAsync(Guid id, UpdateQuestionRequest request, Guid callerUserId, string callerRole, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct);
    Task<QuestionResponse> TogglePublishAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Việc 8 — sửa lại phạm vi hiển thị của 1 câu hỏi đã có (kể cả câu tạo trước Việc 8).
    /// Rà soát Lần VIII — thêm callerUserId để kiểm ownership câu hỏi, không chỉ ownership Lớp.</summary>
    Task<QuestionResponse> UpdateLopVisibilityAsync(Guid id, List<Guid> lopIds, Guid callerUserId, string callerRole, CancellationToken ct);
}
