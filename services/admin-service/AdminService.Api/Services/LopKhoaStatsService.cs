using AdminService.Api.Clients;
using AdminService.Api.Dtos;
using Shared.Contracts;

namespace AdminService.Api.Services;

public sealed class LopKhoaStatsService(IAuthAdminClient authClient, IQuizStatsClient quizStatsClient) : ILopKhoaStatsService
{
    public async Task<LopDiemTrungBinhResponse> GetLopStatsAsync(Guid lopId, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var lop = await authClient.GetLopAsync(lopId, ct);

        if (callerRole != Roles.Admin && lop.GiaoVienId != callerUserId)
        {
            throw new UnauthorizedAccessException("Chỉ Admin hoặc giáo viên chủ nhiệm của lớp này mới được xem điểm lớp.");
        }

        var students = await authClient.ListHocVienAsync(lopId, ct);
        var scores = await GetScoresAsync(students, ct);
        var (avgExam, totalExam, avgPractice, totalPractice) = Aggregate(scores);
        var hocVien = BuildHocVienList(students, scores);

        return new LopDiemTrungBinhResponse(lop.Id, lop.Ten, students.Count, avgExam, totalExam, avgPractice, totalPractice, hocVien);
    }

    public async Task<KhoaDiemTrungBinhResponse> GetKhoaStatsAsync(Guid khoaId, CancellationToken ct)
    {
        var khoa = await authClient.GetKhoaAsync(khoaId, ct);
        var students = await authClient.ListUsersAsync(Roles.Student, null, khoaId, ct);
        var scores = await GetScoresAsync(students, ct);
        var (avgExam, totalExam, avgPractice, totalPractice) = Aggregate(scores);

        return new KhoaDiemTrungBinhResponse(khoa.Id, khoa.Ten, students.Count, avgExam, totalExam, avgPractice, totalPractice);
    }

    private async Task<IReadOnlyList<RemoteUserScore>> GetScoresAsync(IReadOnlyList<RemoteUser> students, CancellationToken ct) =>
        students.Count == 0 ? [] : await quizStatsClient.GetScoresByUsersAsync(students.Select(s => s.Id).ToList(), ct);

    private static IReadOnlyList<HocVienDiemResponse> BuildHocVienList(IReadOnlyList<RemoteUser> students, IReadOnlyList<RemoteUserScore> scores)
    {
        var byUser = scores.ToDictionary(s => s.UserId);
        return students
            .Select(s =>
            {
                byUser.TryGetValue(s.Id, out var sc);
                return new HocVienDiemResponse(s.Id, s.Name, s.ChucVu, sc?.AvgExamScore, sc?.ExamAttempts ?? 0, sc?.AvgPracticeScore, sc?.PracticeAttempts ?? 0);
            })
            .OrderBy(h => h.HoTen)
            .ToList();
    }

    private static (decimal? AvgExam, int TotalExam, decimal? AvgPractice, int TotalPractice) Aggregate(IReadOnlyList<RemoteUserScore> scores)
    {
        if (scores.Count == 0)
        {
            return (null, 0, null, 0);
        }

        var examScores = scores.Where(s => s.AvgExamScore is not null).Select(s => s.AvgExamScore!.Value).ToList();
        var practiceScores = scores.Where(s => s.AvgPracticeScore is not null).Select(s => s.AvgPracticeScore!.Value).ToList();

        var avgExam = examScores.Count > 0 ? Math.Round(examScores.Average(), 2) : (decimal?)null;
        var avgPractice = practiceScores.Count > 0 ? Math.Round(practiceScores.Average(), 2) : (decimal?)null;
        var totalExam = scores.Sum(s => s.ExamAttempts);
        var totalPractice = scores.Sum(s => s.PracticeAttempts);

        return (avgExam, totalExam, avgPractice, totalPractice);
    }
}
