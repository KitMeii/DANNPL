using AdminService.Api.Caching;
using AdminService.Api.Clients;
using AdminService.Api.Dtos;
using Shared.Contracts;

namespace AdminService.Api.Services;

/// <summary>Việc 7 (2026-08-16) — Dashboard "Theo dõi Giáo viên": mỗi giáo viên kèm số lớp, tổng
/// học viên, điểm TB (thi thử/luyện tập) tổng hợp qua các lớp họ chủ nhiệm, số câu hỏi/tài liệu đã
/// tạo. Không có tính năng so sánh/xếp hạng giữa các giáo viên (quyết định nghiệp vụ nhạy cảm, cố
/// ý bỏ qua theo yêu cầu — xem báo cáo Việc 7) — chỉ liệt kê thông tin, sắp theo tên.
///
/// Tổng hợp từ 3 service khác (auth/quiz/content) nên cache 5 phút qua ResponseCache — tránh mỗi
/// lần mở Dashboard lại tính lại từ đầu (users + lớp + học viên + điểm số + nội dung, nhiều lượt
/// gọi mạng cross-service).</summary>
public sealed class TeacherOverviewService(
    IAuthAdminClient authClient,
    IQuizStatsClient quizStatsClient,
    ISystemStatsClient statsClient,
    ResponseCache cache) : ITeacherOverviewService
{
    public Task<IReadOnlyList<TeacherOverviewResponse>> GetTeacherOverviewAsync(CancellationToken ct) =>
        cache.GetOrCreateAsync("teacher-overview", "all", () => ComputeTeacherOverviewAsync(ct));

    public Task<IReadOnlyList<ChapterQuestionCountResponse>> GetQuestionCountsByChapterAsync(CancellationToken ct) =>
        cache.GetOrCreateAsync("question-counts-by-chapter", "all", () => ComputeChapterCountsAsync(ct));

    private async Task<IReadOnlyList<TeacherOverviewResponse>> ComputeTeacherOverviewAsync(CancellationToken ct)
    {
        var teachers = await authClient.ListUsersAsync(Roles.Teacher, null, null, ct);
        if (teachers.Count == 0)
        {
            return [];
        }

        var allLop = await authClient.ListLopAsync(ct);
        var allStudents = await authClient.ListUsersAsync(Roles.Student, null, null, ct);
        var contentByCreator = await statsClient.GetContentCountsByCreatorAsync(ct);

        var lopByTeacher = allLop.Where(l => l.GiaoVienId.HasValue).ToLookup(l => l.GiaoVienId!.Value);
        var studentsByLop = allStudents.Where(s => s.LopId.HasValue).ToLookup(s => s.LopId!.Value);

        // 1 lần gọi batch duy nhất cho MỌI giáo viên (không phải N lần, 1 lần/giáo viên) — tránh
        // N+1 round-trip cross-service, đúng tinh thần "tránh nhiều JOIN cross-service tốn kém".
        var allRelevantStudentIds = teachers
            .SelectMany(t => lopByTeacher[t.Id])
            .SelectMany(l => studentsByLop[l.Id])
            .Select(s => s.Id)
            .Distinct()
            .ToList();
        var allScores = allRelevantStudentIds.Count > 0
            ? await quizStatsClient.GetScoresByUsersAsync(allRelevantStudentIds, ct)
            : [];
        var scoresByUserId = allScores.ToDictionary(s => s.UserId);

        var result = new List<TeacherOverviewResponse>();
        foreach (var teacher in teachers)
        {
            var lops = lopByTeacher[teacher.Id].ToList();
            var studentIds = lops.SelectMany(l => studentsByLop[l.Id]).Select(s => s.Id).Distinct().ToList();
            var teacherScores = studentIds
                .Select(id => scoresByUserId.GetValueOrDefault(id))
                .Where(s => s is not null)
                .ToList();

            var examScores = teacherScores.Where(s => s!.AvgExamScore is not null).Select(s => s!.AvgExamScore!.Value).ToList();
            var avgExam = examScores.Count > 0 ? Math.Round(examScores.Average(), 2) : (decimal?)null;

            contentByCreator.TryGetValue(teacher.Id, out var content);

            result.Add(new TeacherOverviewResponse(
                teacher.Id, teacher.Name, lops.Count, studentIds.Count, avgExam,
                content?.QuestionCount ?? 0, content?.MaterialCount ?? 0));
        }

        return result.OrderBy(t => t.Name).ToList();
    }

    private async Task<IReadOnlyList<ChapterQuestionCountResponse>> ComputeChapterCountsAsync(CancellationToken ct)
    {
        var counts = await statsClient.GetQuestionCountsByChapterAsync(ct);
        return counts
            .Select(kv => new ChapterQuestionCountResponse(kv.Key, kv.Value))
            .OrderByDescending(c => c.Count)
            .ToList();
    }

    /// <summary>Việc D — không cache (khác 2 hàm trên): chỉ gọi khi Admin chủ động mở rộng 1 dòng
    /// giáo viên cụ thể, tần suất thấp và luôn muốn dữ liệu mới nhất của đúng giáo viên đó.</summary>
    public async Task<IReadOnlyList<LopQualityResponse>> GetTeacherLopQualityAsync(Guid teacherId, CancellationToken ct)
    {
        var allLop = await authClient.ListLopAsync(ct);
        var teacherLops = allLop.Where(l => l.GiaoVienId == teacherId).OrderBy(l => l.Ten).ToList();
        if (teacherLops.Count == 0)
        {
            return [];
        }

        var rosterByLop = new Dictionary<Guid, IReadOnlyList<RemoteUser>>();
        foreach (var lop in teacherLops)
        {
            rosterByLop[lop.Id] = await authClient.ListHocVienAsync(lop.Id, ct);
        }

        var allStudentIds = rosterByLop.Values.SelectMany(r => r.Select(u => u.Id)).Distinct().ToList();
        var allScores = allStudentIds.Count > 0
            ? await quizStatsClient.GetScoresByUsersAsync(allStudentIds, ct)
            : [];
        var scoresByUserId = allScores.ToDictionary(s => s.UserId);

        return teacherLops.Select(lop =>
        {
            var roster = rosterByLop[lop.Id];
            var scores = roster.Select(u => scoresByUserId.GetValueOrDefault(u.Id)).Where(s => s is not null).ToList();
            var examScores = scores.Where(s => s!.AvgExamScore is not null).Select(s => s!.AvgExamScore!.Value).ToList();
            var avgExam = examScores.Count > 0 ? Math.Round(examScores.Average(), 2) : (decimal?)null;
            return new LopQualityResponse(lop.Id, lop.Ten, roster.Count, avgExam);
        }).ToList();
    }
}
