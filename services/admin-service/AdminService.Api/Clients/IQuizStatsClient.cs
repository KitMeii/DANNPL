namespace AdminService.Api.Clients;

/// <summary>Điểm Kiểm tra (exam) — xem remarks ở quiz-service's UserScoreSummary. Avg = null nghĩa
/// là chưa có lượt nào.</summary>
public sealed record RemoteUserScore(Guid UserId, decimal? AvgExamScore, int ExamAttempts);

/// <summary>Gọi endpoint batch điểm mới ở quiz-service (Bước C) — dùng để tổng hợp điểm TB theo
/// Lớp/Khóa. Endpoint đích yêu cầu X-Internal-Key (service-to-service, nhận nguyên 1 danh sách
/// UserId chứ không tự suy ra từ JWT người gọi).</summary>
public interface IQuizStatsClient
{
    Task<IReadOnlyList<RemoteUserScore>> GetScoresByUsersAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);
}
