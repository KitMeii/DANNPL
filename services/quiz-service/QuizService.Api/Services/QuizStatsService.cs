using Microsoft.EntityFrameworkCore;
using QuizService.Api.Clients;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using Shared.Contracts;

namespace QuizService.Api.Services;

public sealed class QuizStatsService(QuizDbContext db, IAuthQuizClient authClient) : IQuizStatsService
{
    public async Task<ScoresByUsersResponse> GetScoresByUsersAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        var distinctIds = userIds.Distinct().ToList();

        var examStats = await db.ExamResults
            .Where(r => distinctIds.Contains(r.UserId))
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Avg = g.Average(r => r.Score), Count = g.Count() })
            .ToListAsync(ct);

        var practiceStats = await db.QuizResults
            .Where(r => distinctIds.Contains(r.UserId))
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Avg = g.Average(r => r.Score), Count = g.Count() })
            .ToListAsync(ct);

        var examByUser = examStats.ToDictionary(x => x.UserId);
        var practiceByUser = practiceStats.ToDictionary(x => x.UserId);

        var summaries = distinctIds.Select(id =>
        {
            examByUser.TryGetValue(id, out var exam);
            practiceByUser.TryGetValue(id, out var practice);

            return new UserScoreSummary(
                id,
                exam is null ? null : Math.Round(exam.Avg, 2),
                exam?.Count ?? 0,
                practice is null ? null : Math.Round(practice.Avg, 2),
                practice?.Count ?? 0);
        }).ToList();

        return new ScoresByUsersResponse(summaries);
    }

    public async Task<LopLeaderboardResponse> GetLopLeaderboardAsync(Guid lopId, Guid callerId, string callerRole, CancellationToken ct)
    {
        if (callerRole == Roles.Student)
        {
            var myLopId = await authClient.GetMyLopIdAsync(ct);
            if (myLopId != lopId)
            {
                throw new UnauthorizedAccessException("Bạn chỉ được xem bảng xếp hạng của lớp mình.");
            }
        }
        else if (callerRole == Roles.Teacher)
        {
            var myLopIds = await authClient.ListMyLopIdsAsync(ct);
            if (!myLopIds.Contains(lopId))
            {
                throw new UnauthorizedAccessException("Bạn chỉ được xem bảng xếp hạng của lớp mình phụ trách.");
            }
        }
        // Admin: không giới hạn — tạm thời, Việc D sẽ quyết định có khóa hẳn trang này với Admin
        // hay không.

        var roster = await authClient.ListHocVienAsync(lopId, ct);
        if (roster.Count == 0)
        {
            return new LopLeaderboardResponse(lopId, []);
        }

        var scores = await GetScoresByUsersAsync(roster.Select(r => r.Id).ToList(), ct);
        var scoresByUser = scores.Users.ToDictionary(u => u.UserId);

        var entries = roster
            .Select(r =>
            {
                scoresByUser.TryGetValue(r.Id, out var s);
                return new LopLeaderboardEntryResponse(r.Id, r.Name, r.ChucVu, r.CapBac, r.AvatarUrl, s?.AvgExamScore, s?.ExamAttempts ?? 0, s?.AvgPracticeScore, s?.PracticeAttempts ?? 0);
            })
            .OrderBy(e => e.Name)
            .ToList();

        return new LopLeaderboardResponse(lopId, entries);
    }
}
