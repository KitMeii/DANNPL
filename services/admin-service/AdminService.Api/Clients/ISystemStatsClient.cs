namespace AdminService.Api.Clients;

public sealed record SystemOverview(int MaterialCount, int QuestionCount, int OralQuestionCount);

/// <summary>Việc 7 (2026-08-16) — số câu hỏi/tài liệu do 1 giáo viên tạo, cho Dashboard "Theo dõi
/// Giáo viên". 0/0 nếu giáo viên đó chưa tạo gì.</summary>
public sealed record ContentCounts(int QuestionCount, int MaterialCount);

/// <summary>Composes a system overview from other services' existing list endpoints (counting
/// the results) rather than each service exposing a bespoke count endpoint — fine at this
/// project's scale; revisit with dedicated aggregate endpoints if the lists get large.</summary>
public interface ISystemStatsClient
{
    Task<SystemOverview> GetOverviewAsync(CancellationToken ct);

    /// <summary>Nhóm theo CreatedBy/UploadedBy — tái dùng đúng 2 list endpoint GetOverviewAsync đã
    /// gọi (chỉ đổi kiểu deserialize để lấy thêm field creator), không thêm endpoint mới ở quiz-
    /// service/content-service.</summary>
    Task<IReadOnlyDictionary<Guid, ContentCounts>> GetContentCountsByCreatorAsync(CancellationToken ct);

    /// <summary>Số câu hỏi TN theo Chương (toàn hệ thống) — cho biểu đồ cột "Câu hỏi theo Chương"
    /// ở Dashboard Admin. Không phân biệt giáo viên nào tạo, chỉ phân bố theo nội dung.</summary>
    Task<IReadOnlyDictionary<string, int>> GetQuestionCountsByChapterAsync(CancellationToken ct);
}
