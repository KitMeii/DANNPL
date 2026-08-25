using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

public interface IEssayQuestionService
{
    // Rà soát Lần XI (2026-08-21) — thêm callerUserId/callerRole: GV chỉ thấy/sửa/xóa/publish được
    // câu tự luận CHÍNH MÌNH tạo (Admin thấy hết) — cùng lỗi đã sửa cho Question/OralQuestion/
    // Material ở Lần VIII nhưng EssayQuestion bị bỏ sót hoàn toàn (Update/Delete/TogglePublish
    // trước đây còn KHÔNG NHẬN callerUserId/callerRole, không thể kiểm ownership dù muốn).
    Task<IReadOnlyList<EssayQuestionResponse>> ListAsync(string? chapter, Guid callerUserId, string callerRole, CancellationToken ct);
    Task<IReadOnlyList<EssayQuestionPracticeResponse>> ListForPracticeAsync(string? chapter, Guid? callerLopId, CancellationToken ct);
    Task<EssayQuestionResponse> CreateAsync(CreateEssayQuestionRequest request, Guid createdBy, string callerRole, CancellationToken ct);
    Task<EssayQuestionResponse> UpdateAsync(Guid id, UpdateEssayQuestionRequest request, Guid callerUserId, string callerRole, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct);
    Task<EssayQuestionResponse> TogglePublishAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct);
    Task<EssayQuestionResponse> UpdateLopVisibilityAsync(Guid id, List<Guid> lopIds, Guid callerUserId, string callerRole, CancellationToken ct);
}
