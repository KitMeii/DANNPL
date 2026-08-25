namespace AdminService.Api.Clients;

/// <summary>Điểm "exam" (thi thử) và "practice" (luyện tập) tách riêng — 2 loại khác ý nghĩa, xem
/// remarks ở quiz-service's UserScoreSummary. Avg = null nghĩa là chưa có lượt nào loại đó.</summary>
public sealed record RemoteUserScore(Guid UserId, decimal? AvgExamScore, int ExamAttempts, decimal? AvgPracticeScore, int PracticeAttempts);

/// <summary>Gọi endpoint batch điểm mới ở quiz-service (Bước C) — dùng để tổng hợp điểm TB theo
/// Lớp/Khóa. Endpoint đích yêu cầu X-Internal-Key (service-to-service, nhận nguyên 1 danh sách
/// UserId chứ không tự suy ra từ JWT người gọi).</summary>
public interface IQuizStatsClient
{
    Task<IReadOnlyList<RemoteUserScore>> GetScoresByUsersAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);
}
