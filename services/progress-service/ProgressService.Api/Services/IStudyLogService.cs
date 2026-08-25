using ProgressService.Api.Dtos;

namespace ProgressService.Api.Services;

public interface IStudyLogService
{
    Task LogTodayAsync(Guid userId, int minutes, CancellationToken ct);
    Task<IReadOnlyList<StudyLogResponse>> GetWeeklyAsync(Guid userId, CancellationToken ct);
}
