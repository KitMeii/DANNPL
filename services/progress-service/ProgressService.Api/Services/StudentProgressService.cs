using Microsoft.EntityFrameworkCore;
using ProgressService.Api.Clients;
using ProgressService.Api.Data;
using ProgressService.Api.Dtos;
using ProgressService.Api.Entities;
using Shared.Contracts;

namespace ProgressService.Api.Services;

public sealed class StudentProgressService(ProgressDbContext db, IUserNameLookupClient nameLookup) : IStudentProgressService
{
    public async Task RecordScoreAsync(Guid userId, decimal score, CancellationToken ct)
    {
        var progress = await db.StudentProgress.FindAsync([userId], ct);
        if (progress is null)
        {
            progress = new StudentProgress { UserId = userId };
            db.StudentProgress.Add(progress);
        }

        progress.TotalAttempts++;
        progress.ScoreSum += score;
        progress.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task<MyProgressResponse> GetMyProgressAsync(Guid userId, CancellationToken ct)
    {
        var progress = await db.StudentProgress.FindAsync([userId], ct);
        if (progress is null)
        {
            return new MyProgressResponse(0, null, 0, 0, 0m);
        }

        return ToResponse(progress);
    }

    /// <summary>Việc B (2026-08-16) — fix bug xác nhận qua ảnh chụp thật: tài khoản Teacher/Admin
    /// (VD tự thử làm bài để kiểm tra ngân hàng câu hỏi) vẫn tạo ra StudentProgress như 1 học viên
    /// bình thường, nên lọt vào bảng xếp hạng. progress-service không lưu Role — phải hỏi
    /// auth-service qua nameLookup (giờ trả kèm Role, xem UserName). QUAN TRỌNG: lọc Role=Student
    /// PHẢI làm TRƯỚC khi cắt còn `top` dòng — lọc sau Take(top) có thể trả về ít hơn top dòng (hoặc
    /// rỗng) nếu vài vị trí đầu bảng là tài khoản không phải Student, đúng y hệt bug đã thấy
    /// ("Giáo viên Demo" chiếm 1 trong 2 vị trí). Không giới hạn số dòng truy vấn trước khi lọc —
    /// cùng mức chấp nhận "phù hợp ở quy mô dự án này" như HttpSystemStatsClient đã áp dụng.</summary>
    public async Task<IReadOnlyList<LeaderboardEntryResponse>> GetLeaderboardAsync(int top, CancellationToken ct)
    {
        var allProgress = await db.StudentProgress
            .Where(p => p.TotalAttempts > 0)
            .OrderByDescending(p => p.ScoreSum / p.TotalAttempts)
            .ThenByDescending(p => p.Streak)
            .ToListAsync(ct);

        var names = await nameLookup.GetNamesAsync(allProgress.Select(p => p.UserId).ToList(), ct);

        return allProgress
            .Where(p => names.TryGetValue(p.UserId, out var u) && u.Role == Roles.Student)
            .Take(top)
            .Select(p => new LeaderboardEntryResponse(
                p.UserId,
                names[p.UserId].Name,
                Math.Round(p.ScoreSum / p.TotalAttempts, 2),
                p.Streak,
                p.TotalAttempts))
            .ToList();
    }

    private static MyProgressResponse ToResponse(StudentProgress p) => new(
        p.Streak,
        p.LastStudyDate,
        p.TotalStudyMinutes,
        p.TotalAttempts,
        p.TotalAttempts > 0 ? Math.Round(p.ScoreSum / p.TotalAttempts, 2) : 0m);
}
