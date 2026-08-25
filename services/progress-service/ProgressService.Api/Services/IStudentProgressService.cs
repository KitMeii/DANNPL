using ProgressService.Api.Dtos;

namespace ProgressService.Api.Services;

public interface IStudentProgressService
{
    Task RecordScoreAsync(Guid userId, decimal score, CancellationToken ct);
    Task<MyProgressResponse> GetMyProgressAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<LeaderboardEntryResponse>> GetLeaderboardAsync(int top, CancellationToken ct);
}
