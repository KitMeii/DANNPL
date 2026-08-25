using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

public interface IQuizStatsService
{
    Task<ScoresByUsersResponse> GetScoresByUsersAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);

    /// <summary>Việc C (2026-08-16) — bảng xếp hạng theo Lớp, xếp theo Điểm TB Thi thử (điểm chính
    /// thức, khó "cày" điểm hơn Luyện tập vì không làm lại nhiều lần). callerId/callerRole tự xác
    /// thực quyền xem đúng lopId này: Student chỉ xem đúng lớp mình, Teacher chỉ xem đúng lớp mình
    /// phụ trách, Admin không giới hạn (tạm thời — Việc D sẽ quyết định có khóa hẳn Admin không).</summary>
    Task<LopLeaderboardResponse> GetLopLeaderboardAsync(Guid lopId, Guid callerId, string callerRole, CancellationToken ct);
}
