using Microsoft.EntityFrameworkCore;
using ProgressService.Api.Data;
using ProgressService.Api.Dtos;

namespace ProgressService.Api.Services;

public sealed class LopDataAdminService(ProgressDbContext db) : ILopDataAdminService
{
    public async Task<ProgressLopDataDumpResponse> DumpAsync(ProgressLopDataRequest request, CancellationToken ct)
    {
        var userIds = request.UserIds;

        var progress = await db.StudentProgress.Where(p => userIds.Contains(p.UserId))
            .Select(p => new StudentProgressDump(p.UserId, p.Streak, p.LastStudyDate, p.TotalStudyMinutes, p.TotalAttempts, p.ScoreSum, p.UpdatedAtUtc))
            .ToListAsync(ct);

        var logs = await db.StudyLogs.Where(l => userIds.Contains(l.UserId))
            .Select(l => new StudyLogDump(l.Id, l.UserId, l.StudyDate, l.Minutes, l.CreatedAtUtc))
            .ToListAsync(ct);

        return new ProgressLopDataDumpResponse(progress, logs);
    }

    public async Task<ProgressLopDataDeleteResponse> DeleteAsync(ProgressLopDataRequest request, CancellationToken ct)
    {
        var userIds = request.UserIds;

        var progressDeleted = await db.StudentProgress.Where(p => userIds.Contains(p.UserId)).ExecuteDeleteAsync(ct);
        var logsDeleted = await db.StudyLogs.Where(l => userIds.Contains(l.UserId)).ExecuteDeleteAsync(ct);

        return new ProgressLopDataDeleteResponse(progressDeleted, logsDeleted);
    }
}
