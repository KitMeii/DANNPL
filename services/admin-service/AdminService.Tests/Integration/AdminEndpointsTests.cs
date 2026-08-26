using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdminService.Api.Clients;
using AdminService.Api.Dtos;
using Shared.Contracts;
using Shared.Infrastructure.Common;
using Xunit;

namespace AdminService.Tests.Integration;

public sealed class AdminEndpointsTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;
    private readonly HttpClient _client;

    public AdminEndpointsTests(AdminApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage WithAuth(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Student_cannot_access_any_admin_endpoint()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/admin/users", TestTokens.Student()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Change_role_persists_and_is_recorded_in_the_audit_log()
    {
        var targetId = Guid.NewGuid();
        _factory.AuthClient.Users.Add(new RemoteUser(targetId, "hocvien@test.local", "Học viên A", Roles.Student));
        var adminToken = TestTokens.Admin();

        var changeRequest = WithAuth(HttpMethod.Put, $"/api/v1/admin/users/{targetId}/role", adminToken);
        changeRequest.Content = JsonContent.Create(new ChangeRoleRequest(Roles.Teacher));
        var changeResponse = await _client.SendAsync(changeRequest);

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
        var changed = (await changeResponse.Content.ReadFromJsonAsync<ApiResponse<UserSummaryResponse>>())!.Data!;
        Assert.Equal(Roles.Teacher, changed.Role);

        var auditRequest = WithAuth(HttpMethod.Get, "/api/v1/admin/audit-log", adminToken);
        var auditResponse = await _client.SendAsync(auditRequest);
        var audit = (await auditResponse.Content.ReadFromJsonAsync<ApiResponse<List<RoleChangeAuditResponse>>>())!.Data!;

        var entry = Assert.Single(audit, a => a.TargetUserId == targetId);
        Assert.Equal(Roles.Student, entry.OldRole);
        Assert.Equal(Roles.Teacher, entry.NewRole);
        // Rà soát Lần XVII (2026-08-21) — audit log giờ resolve sẵn tên (trước chỉ có Id thô).
        Assert.Equal("Học viên A", entry.TargetName);
    }

    [Fact]
    public async Task Change_role_for_unknown_user_returns_404()
    {
        var request = WithAuth(HttpMethod.Put, $"/api/v1/admin/users/{Guid.NewGuid()}/role", TestTokens.Admin());
        request.Content = JsonContent.Create(new ChangeRoleRequest(Roles.Teacher));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task System_config_can_be_set_and_read_back()
    {
        var adminToken = TestTokens.Admin();

        var setRequest = WithAuth(HttpMethod.Put, "/api/v1/admin/config/registration_enabled", adminToken);
        setRequest.Content = JsonContent.Create(new SetConfigRequest("true"));
        await _client.SendAsync(setRequest);

        var getRequest = WithAuth(HttpMethod.Get, "/api/v1/admin/config", adminToken);
        var getResponse = await _client.SendAsync(getRequest);
        var configs = (await getResponse.Content.ReadFromJsonAsync<ApiResponse<List<SystemConfigResponse>>>())!.Data!;

        var entry = Assert.Single(configs, c => c.Key == "registration_enabled");
        Assert.Equal("true", entry.Value);
    }

    [Fact]
    public async Task Overview_combines_user_role_counts_with_content_and_quiz_stats()
    {
        _factory.AuthClient.Users.Clear();
        _factory.AuthClient.Users.Add(new RemoteUser(Guid.NewGuid(), "s1@test.local", "S1", Roles.Student));
        _factory.AuthClient.Users.Add(new RemoteUser(Guid.NewGuid(), "s2@test.local", "S2", Roles.Student));
        _factory.AuthClient.Users.Add(new RemoteUser(Guid.NewGuid(), "t1@test.local", "T1", Roles.Teacher));
        _factory.StatsClient.Overview = new SystemOverview(MaterialCount: 5, QuestionCount: 40, OralQuestionCount: 10);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/admin/stats/overview", TestTokens.Admin()));
        var overview = (await response.Content.ReadFromJsonAsync<ApiResponse<SystemOverviewResponse>>())!.Data!;

        Assert.Equal(2, overview.TotalStudents);
        Assert.Equal(1, overview.TotalTeachers);
        Assert.Equal(5, overview.TotalMaterials);
        Assert.Equal(40, overview.TotalQuestions);
        Assert.Equal(10, overview.TotalOralQuestions);
    }

    [Fact]
    public async Task List_users_forwards_lopId_filter_and_includes_chuc_vu()
    {
        var lopId = Guid.NewGuid();
        var inLop = Guid.NewGuid();
        _factory.AuthClient.Users.Add(new RemoteUser(inLop, "inlop@test.local", "In Lop", Roles.Student, LopId: lopId, ChucVu: "Lớp trưởng", CapBac: "Trung sĩ"));
        _factory.AuthClient.Users.Add(new RemoteUser(Guid.NewGuid(), "outside@test.local", "Outside", Roles.Student));

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/users?lopId={lopId}", TestTokens.Admin()));
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<UserSummaryResponse>>>())!.Data!;

        var only = Assert.Single(list);
        Assert.Equal(inLop, only.Id);
        Assert.Equal("Lớp trưởng", only.ChucVu);
        // Việc V (2026-08-20) — lopDetailModal hợp nhất cần CapBac ngay trong danh sách roster, phải
        // xác nhận field này không bị rớt qua tầng admin-service (bug tương tự đã gặp ở auth-service
        // ToUserResponse trước đó, xem KhoaLopService.cs).
        Assert.Equal("Trung sĩ", only.CapBac);
    }

    // ═══════════════ Việc 7 (2026-08-16) — Dashboard "Theo dõi Giáo viên" ═══════════════
    // Endpoint dùng ResponseCache (khóa cố định "teacher-overview"/"all") — mỗi test class instance
    // (AdminApiFactory) chỉ nên gọi GET /stats/teachers ĐÚNG 1 LẦN trong toàn bộ class, kẻo test
    // sau đọc nhầm cache của test trước (IClassFixture dùng chung 1 factory/1 cache cho cả class).
    // Gộp mọi assertion cần thiết vào 1 test seed-1-lần-fetch-1-lần duy nhất.

    [Fact]
    public async Task Student_cannot_view_teacher_overview()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/admin/stats/teachers", TestTokens.Student()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_overview_aggregates_lop_students_scores_and_content_per_teacher()
    {
        _factory.AuthClient.Users.Clear();
        _factory.AuthClient.Lops.Clear();

        var teacherWithLop = Guid.NewGuid();
        var teacherWithoutLop = Guid.NewGuid();
        var lop1 = Guid.NewGuid();
        var lop2 = Guid.NewGuid();
        var khoaId = Guid.NewGuid();
        var student1 = Guid.NewGuid();
        var student2 = Guid.NewGuid();
        var student3 = Guid.NewGuid(); // lớp 2, chưa có lượt làm bài nào

        _factory.AuthClient.Users.Add(new RemoteUser(teacherWithLop, "gv1@test.local", "Giáo viên B", Roles.Teacher));
        _factory.AuthClient.Users.Add(new RemoteUser(teacherWithoutLop, "gv2@test.local", "Giáo viên A", Roles.Teacher));
        _factory.AuthClient.Users.Add(new RemoteUser(student1, "sv1@test.local", "SV1", Roles.Student, LopId: lop1));
        _factory.AuthClient.Users.Add(new RemoteUser(student2, "sv2@test.local", "SV2", Roles.Student, LopId: lop2));
        _factory.AuthClient.Users.Add(new RemoteUser(student3, "sv3@test.local", "SV3", Roles.Student, LopId: lop2));
        _factory.AuthClient.Lops[lop1] = new RemoteLop(lop1, "Lớp 1", khoaId, teacherWithLop);
        _factory.AuthClient.Lops[lop2] = new RemoteLop(lop2, "Lớp 2", khoaId, teacherWithLop);

        _factory.QuizStatsClient.Scores[student1] = new RemoteUserScore(student1, 8m, 2);
        _factory.QuizStatsClient.Scores[student2] = new RemoteUserScore(student2, 10m, 1);
        // student3: không seed -> FakeQuizStatsClient tự trả null/0, đúng hành vi "chưa làm bài nào"

        _factory.StatsClient.ContentCountsByCreator[teacherWithLop] = new ContentCounts(QuestionCount: 12, MaterialCount: 3);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/admin/stats/teachers", TestTokens.Admin()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<TeacherOverviewResponse>>>())!.Data!;

        Assert.Equal(2, list.Count);
        // Sắp theo TÊN, không theo điểm/hiệu suất — "Giáo viên A" (không có lớp) phải đứng trước
        // "Giáo viên B" dù B có điểm cao hơn, xác nhận đây không phải bảng xếp hạng.
        Assert.Equal(["Giáo viên A", "Giáo viên B"], list.Select(t => t.Name));

        var withLop = list.Single(t => t.TeacherId == teacherWithLop);
        Assert.Equal(2, withLop.LopCount);
        Assert.Equal(3, withLop.TotalStudents);
        Assert.Equal(9m, withLop.AvgExamScore); // (8+10)/2, student3 không có điểm exam nên bị loại khỏi trung bình
        Assert.Equal(12, withLop.QuestionCount);
        Assert.Equal(3, withLop.MaterialCount);

        var withoutLop = list.Single(t => t.TeacherId == teacherWithoutLop);
        Assert.Equal(0, withoutLop.LopCount);
        Assert.Equal(0, withoutLop.TotalStudents);
        Assert.Null(withoutLop.AvgExamScore);
        Assert.Equal(0, withoutLop.QuestionCount);
        Assert.Equal(0, withoutLop.MaterialCount);
    }

    // ═══════════════ Việc D (2026-08-16) — drill-down từng Lớp của 1 giáo viên ═══════════════

    [Fact]
    public async Task Student_cannot_view_teacher_lop_quality()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/teachers/{Guid.NewGuid()}/lop-quality", TestTokens.Student()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Lop_quality_breaks_down_scores_per_lop_instead_of_blending_them()
    {
        _factory.AuthClient.Users.Clear();
        _factory.AuthClient.Lops.Clear();

        var teacherId = Guid.NewGuid();
        var strongLop = Guid.NewGuid();
        var weakLop = Guid.NewGuid();
        var khoaId = Guid.NewGuid();
        var strongStudent = Guid.NewGuid();
        var weakStudent = Guid.NewGuid();

        _factory.AuthClient.Users.Add(new RemoteUser(teacherId, "gv-lopquality@test.local", "GV Lớp Quality", Roles.Teacher));
        _factory.AuthClient.Users.Add(new RemoteUser(strongStudent, "strong@test.local", "SV Giỏi", Roles.Student, LopId: strongLop));
        _factory.AuthClient.Users.Add(new RemoteUser(weakStudent, "weak@test.local", "SV Yếu", Roles.Student, LopId: weakLop));
        // Lớp B đặt tên trước Lớp A theo alphabet để xác nhận sắp theo tên, không theo điểm.
        _factory.AuthClient.Lops[strongLop] = new RemoteLop(strongLop, "Lớp B Giỏi", khoaId, teacherId);
        _factory.AuthClient.Lops[weakLop] = new RemoteLop(weakLop, "Lớp A Yếu", khoaId, teacherId);

        _factory.QuizStatsClient.Scores[strongStudent] = new RemoteUserScore(strongStudent, 9m, 3);
        _factory.QuizStatsClient.Scores[weakStudent] = new RemoteUserScore(weakStudent, 3m, 3);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/teachers/{teacherId}/lop-quality", TestTokens.Admin()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<LopQualityResponse>>>())!.Data!;

        Assert.Equal(2, list.Count);
        Assert.Equal(["Lớp A Yếu", "Lớp B Giỏi"], list.Select(l => l.LopTen)); // sắp theo tên

        var strong = list.Single(l => l.LopId == strongLop);
        Assert.Equal(1, strong.StudentCount);
        Assert.Equal(9m, strong.AvgExamScore);

        var weak = list.Single(l => l.LopId == weakLop);
        Assert.Equal(1, weak.StudentCount);
        Assert.Equal(3m, weak.AvgExamScore);
    }

    [Fact]
    public async Task Lop_quality_for_teacher_with_no_lop_returns_empty_list()
    {
        _factory.AuthClient.Users.Clear();
        _factory.AuthClient.Lops.Clear();
        var teacherId = Guid.NewGuid();
        _factory.AuthClient.Users.Add(new RemoteUser(teacherId, "gv-nolop@test.local", "GV Không Lớp", Roles.Teacher));

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/teachers/{teacherId}/lop-quality", TestTokens.Admin()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<LopQualityResponse>>>())!.Data!;
        Assert.Empty(list);
    }

    [Fact]
    public async Task Student_cannot_view_questions_by_chapter_stats()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/admin/stats/questions-by-chapter", TestTokens.Student()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Questions_by_chapter_returns_counts_sorted_descending()
    {
        _factory.StatsClient.QuestionCountsByChapter["Chương 1"] = 5;
        _factory.StatsClient.QuestionCountsByChapter["Chương 2"] = 20;
        _factory.StatsClient.QuestionCountsByChapter["Chương 3"] = 10;

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/admin/stats/questions-by-chapter", TestTokens.Admin()));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<ChapterQuestionCountResponse>>>())!.Data!;

        Assert.Equal(["Chương 2", "Chương 3", "Chương 1"], list.Select(c => c.Chapter));
        Assert.Equal([20, 10, 5], list.Select(c => c.Count));
    }

    // ═══ Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp + khóa/mở khóa tài khoản ═══

    [Fact]
    public async Task Admin_can_create_a_user_directly()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/admin/users", TestTokens.Admin());
        request.Content = JsonContent.Create(new CreateUserRequest("gv-moi@test.local", "P@ssw0rd123", "GV Mới", Roles.Teacher));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ApiResponse<UserSummaryResponse>>())!.Data!;
        Assert.Equal(Roles.Teacher, created.Role);
        Assert.Contains(_factory.AuthClient.Users, u => u.Email == "gv-moi@test.local");
    }

    [Fact]
    public async Task Creating_a_user_with_a_taken_email_returns_conflict()
    {
        _factory.AuthClient.Users.Add(new RemoteUser(Guid.NewGuid(), "trung@test.local", "Đã tồn tại", Roles.Student));

        var request = WithAuth(HttpMethod.Post, "/api/v1/admin/users", TestTokens.Admin());
        request.Content = JsonContent.Create(new CreateUserRequest("trung@test.local", "P@ssw0rd123", "X", Roles.Student));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_create_a_user()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/admin/users", TestTokens.Student());
        request.Content = JsonContent.Create(new CreateUserRequest("x@test.local", "P@ssw0rd123", "X", Roles.Student));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_lock_and_unlock_a_user()
    {
        var targetId = Guid.NewGuid();
        _factory.AuthClient.Users.Add(new RemoteUser(targetId, "hocvien-lock@test.local", "Học viên B", Roles.Student));

        var lockRequest = WithAuth(HttpMethod.Put, $"/api/v1/admin/users/{targetId}/locked", TestTokens.Admin());
        lockRequest.Content = JsonContent.Create(new SetUserLockedRequest(true));
        var lockResponse = await _client.SendAsync(lockRequest);

        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);
        Assert.True((await lockResponse.Content.ReadFromJsonAsync<ApiResponse<UserSummaryResponse>>())!.Data!.IsLocked);
        Assert.True(_factory.AuthClient.Users.Single(u => u.Id == targetId).IsLocked);

        var unlockRequest = WithAuth(HttpMethod.Put, $"/api/v1/admin/users/{targetId}/locked", TestTokens.Admin());
        unlockRequest.Content = JsonContent.Create(new SetUserLockedRequest(false));
        var unlockResponse = await _client.SendAsync(unlockRequest);

        Assert.False((await unlockResponse.Content.ReadFromJsonAsync<ApiResponse<UserSummaryResponse>>())!.Data!.IsLocked);
    }

    [Fact]
    public async Task Locking_an_unknown_user_returns_404()
    {
        var request = WithAuth(HttpMethod.Put, $"/api/v1/admin/users/{Guid.NewGuid()}/locked", TestTokens.Admin());
        request.Content = JsonContent.Create(new SetUserLockedRequest(true));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_lock_a_user()
    {
        var request = WithAuth(HttpMethod.Put, $"/api/v1/admin/users/{Guid.NewGuid()}/locked", TestTokens.Student());
        request.Content = JsonContent.Create(new SetUserLockedRequest(true));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
