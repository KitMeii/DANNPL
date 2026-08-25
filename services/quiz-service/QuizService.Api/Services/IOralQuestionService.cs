using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

public interface IOralQuestionService
{
    /// <summary>Rà soát Lần VIII (2026-08-21) — song song QuestionService.ListAsync: Teacher chỉ
    /// thấy câu vấn đáp chính mình tạo, Admin thấy hết.</summary>
    Task<IReadOnlyList<OralQuestionResponse>> ListAsync(string? chapter, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Việc 4.4 Phần A — callerLopId lọc theo Lớp của người gọi (null = học viên chưa gán
    /// Lớp, chỉ thấy câu toàn hệ thống), cùng công thức với QuestionService.ListForPracticeAsync.
    /// KHÔNG lọc theo người tạo — luồng học viên, xem remarks ở QuestionService tương ứng.</summary>
    Task<IReadOnlyList<OralQuestionPracticeResponse>> ListForPracticeAsync(string? chapter, Guid? callerLopId, CancellationToken ct);

    Task<OralQuestionResponse> CreateAsync(CreateOralQuestionRequest request, Guid createdBy, string callerRole, CancellationToken ct);

    /// <summary>Rà soát Lần VIII — chỉ người tạo hoặc Admin được sửa/xóa.</summary>
    Task<OralQuestionResponse> UpdateAsync(Guid id, UpdateOralQuestionRequest request, Guid callerUserId, string callerRole, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Việc 4.4 Phần A — song song QuestionService.UpdateLopVisibilityAsync, xem remarks ở đó.</summary>
    Task<OralQuestionResponse> UpdateLopVisibilityAsync(Guid id, List<Guid> lopIds, Guid callerUserId, string callerRole, CancellationToken ct);
}
