using Microsoft.EntityFrameworkCore;
using ProgressService.Api.Data;
using ProgressService.Api.Dtos;
using ProgressService.Api.Entities;

namespace ProgressService.Api.Services;

public sealed class StudyLogService(ProgressDbContext db) : IStudyLogService
{
    public async Task LogTodayAsync(Guid userId, int minutes, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var todayLog = await db.StudyLogs.SingleOrDefaultAsync(s => s.UserId == userId && s.StudyDate == today, ct);
        if (todayLog is null)
        {
            db.StudyLogs.Add(new StudyLog { UserId = userId, StudyDate = today, Minutes = minutes });
        }
        else
        {
            todayLog.Minutes += minutes;
        }

        var progress = await db.StudentProgress.FindAsync([userId], ct);
        if (progress is null)
        {
            progress = new StudentProgress { UserId = userId, Streak = 1, LastStudyDate = today };
            db.StudentProgress.Add(progress);
        }
        else if (progress.LastStudyDate != today)
        {
            // Streak continues only on a genuinely consecutive day; any gap (including the very
            // first log) resets it to 1 rather than silently carrying over a stale count.
            progress.Streak = progress.LastStudyDate == today.AddDays(-1) ? progress.Streak + 1 : 1;
            progress.LastStudyDate = today;
        }

        progress.TotalStudyMinutes += minutes;
        progress.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StudyLogResponse>> GetWeeklyAsync(Guid userId, CancellationToken ct)
    {
        var since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-6);

        return await db.StudyLogs
            .Where(s => s.UserId == userId && s.StudyDate >= since)
            .OrderBy(s => s.StudyDate)
            .Select(s => new StudyLogResponse(s.StudyDate, s.Minutes))
            .ToListAsync(ct);
    }
}
