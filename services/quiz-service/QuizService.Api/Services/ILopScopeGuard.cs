namespace QuizService.Api.Services;

/// <summary>Việc 8 (2026-08-16) — 1 chỗ duy nhất kiểm tra "giáo viên chỉ được giới hạn phạm vi câu
/// hỏi/bộ đề tới Lớp mình chủ nhiệm, Admin thì tới Lớp bất kỳ", dùng chung cho Question,
/// EssayQuestion và ExamVersion thay vì lặp lại logic 6+ nơi.</summary>
public interface ILopScopeGuard
{
    /// <summary>Ném UnauthorizedAccessException (403) nếu callerRole là Teacher và lopIds chứa bất
    /// kỳ Lớp nào ngoài (các) Lớp caller chủ nhiệm. Admin luôn qua. Danh sách rỗng luôn qua (nghĩa
    /// là "toàn hệ thống", không cần kiểm tra Lớp nào).</summary>
    Task EnsureCanAssignAsync(IReadOnlyList<Guid> lopIds, string callerRole, CancellationToken ct);
}
