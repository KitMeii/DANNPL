using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdminService.Api.Clients;
using AdminService.Api.Dtos;
using Shared.Contracts;
using Shared.Infrastructure.Common;
using Xunit;

namespace AdminService.Tests.Integration;

public sealed class LopKhoaStatsEndpointsTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;
    private readonly HttpClient _client;

    public LopKhoaStatsEndpointsTests(AdminApiFactory factory)
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

    private (Guid KhoaId, Guid LopId) SeedKhoaAndLop(string khoaTen, string lopTen)
    {
        var khoaId = Guid.NewGuid();
        var lopId = Guid.NewGuid();
        _factory.AuthClient.Khoas[khoaId] = new RemoteKhoa(khoaId, khoaTen);
        _factory.AuthClient.Lops[lopId] = new RemoteLop(lopId, lopTen, khoaId, null);
        return (khoaId, lopId);
    }

    private Guid SeedStudentInLop(Guid lopId, string name)
    {
        var id = Guid.NewGuid();
        _factory.AuthClient.Users.Add(new RemoteUser(id, $"{name}@test.local", name, Roles.Student, LopId: lopId));
        return id;
    }

    [Fact]
    public async Task Lop_stats_aggregates_exam_scores_across_students()
    {
        var (_, lopId) = SeedKhoaAndLop("K-Test1", "Lop-Test1");
        var s1 = SeedStudentInLop(lopId, "SV1");
        var s2 = SeedStudentInLop(lopId, "SV2");
        // SV3 chưa làm bài nào — không có entry trong Scores, phải bị bỏ qua khi tính trung bình,
        // không kéo trung bình về 0.
        var s3 = SeedStudentInLop(lopId, "SV3");

        _factory.QuizStatsClient.Scores[s1] = new RemoteUserScore(s1, AvgExamScore: 8m, ExamAttempts: 2);
        _factory.QuizStatsClient.Scores[s2] = new RemoteUserScore(s2, AvgExamScore: 6m, ExamAttempts: 1);
        _ = s3;

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/lop/{lopId}", TestTokens.Admin()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopDiemTrungBinhResponse>>())!.Data!;

        Assert.Equal(lopId, body.LopId);
        Assert.Equal("Lop-Test1", body.LopTen);
        Assert.Equal(3, body.TongHocVien);
        // (8 + 6) / 2 = 7 — chỉ SV1/SV2 có điểm thi, SV3 không kéo trung bình xuống.
        Assert.Equal(7m, body.DiemTBThiThu);
        Assert.Equal(3, body.TongLuotThiThu); // 2 + 1
    }

    [Fact]
    public async Task Lop_with_no_students_returns_null_averages_not_zero()
    {
        var (_, lopId) = SeedKhoaAndLop("K-Empty", "Lop-Empty");

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/lop/{lopId}", TestTokens.Admin()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopDiemTrungBinhResponse>>())!.Data!;

        Assert.Equal(0, body.TongHocVien);
        Assert.Null(body.DiemTBThiThu);
        Assert.Equal(0, body.TongLuotThiThu);
    }

    [Fact]
    public async Task Unknown_lop_returns_not_found()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/lop/{Guid.NewGuid()}", TestTokens.Admin()));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Khoa_stats_aggregates_across_multiple_lop()
    {
        var (khoaId, lop1) = SeedKhoaAndLop("K-Multi", "Lop-A");
        var lop2 = Guid.NewGuid();
        _factory.AuthClient.Lops[lop2] = new RemoteLop(lop2, "Lop-B", khoaId, null);

        var s1 = SeedStudentInLop(lop1, "SV-A1");
        var s2 = SeedStudentInLop(lop2, "SV-B1");

        _factory.QuizStatsClient.Scores[s1] = new RemoteUserScore(s1, AvgExamScore: 9m, ExamAttempts: 1);
        _factory.QuizStatsClient.Scores[s2] = new RemoteUserScore(s2, AvgExamScore: 5m, ExamAttempts: 1);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/khoa/{khoaId}", TestTokens.Admin()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<KhoaDiemTrungBinhResponse>>())!.Data!;

        Assert.Equal(khoaId, body.KhoaId);
        Assert.Equal(2, body.TongHocVien); // học viên của cả 2 lớp trong khóa
        Assert.Equal(7m, body.DiemTBThiThu); // (9 + 5) / 2
        Assert.Equal(2, body.TongLuotThiThu);
    }

    [Fact]
    public async Task Unknown_khoa_returns_not_found()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/khoa/{Guid.NewGuid()}", TestTokens.Admin()));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_access_lop_stats()
    {
        var (_, lopId) = SeedKhoaAndLop("K-Forbidden", "Lop-Forbidden");
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/lop/{lopId}", TestTokens.Student()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Lop_stats_includes_per_student_breakdown()
    {
        var (_, lopId) = SeedKhoaAndLop("K-PerStudent", "Lop-PerStudent");
        var s1 = SeedStudentInLop(lopId, "SV-Detail1");
        var s2 = SeedStudentInLop(lopId, "SV-Detail2"); // chưa làm bài nào

        _factory.QuizStatsClient.Scores[s1] = new RemoteUserScore(s1, AvgExamScore: 8m, ExamAttempts: 2);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/lop/{lopId}", TestTokens.Admin()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopDiemTrungBinhResponse>>())!.Data!;

        Assert.Equal(2, body.HocVien.Count);
        var hv1 = body.HocVien.Single(h => h.UserId == s1);
        Assert.Equal(8m, hv1.DiemThiThu);
        Assert.Equal(2, hv1.SoLuotThiThu);

        var hv2 = body.HocVien.Single(h => h.UserId == s2);
        Assert.Null(hv2.DiemThiThu);
        Assert.Equal(0, hv2.SoLuotThiThu);
    }

    // ---------------------------------------------------------------------------
    // Gap 2 — Teacher xem điểm lớp mình phụ trách (RBAC 2 chiều)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Chu_nhiem_can_view_stats_of_their_own_lop()
    {
        var (_, lopId) = SeedKhoaAndLop("K-ChuNhiem", "Lop-ChuNhiem");
        var teacherId = Guid.NewGuid();
        _factory.AuthClient.Lops[lopId] = _factory.AuthClient.Lops[lopId] with { GiaoVienId = teacherId };
        SeedStudentInLop(lopId, "SV-CN1");

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/lop/{lopId}", TestTokens.Teacher(teacherId)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopDiemTrungBinhResponse>>())!.Data!;
        Assert.Equal(lopId, body.LopId);
    }

    // Trọng tâm test RBAC 2 chiều theo yêu cầu: Teacher A gọi lopId của lớp Teacher B phụ trách
    // phải bị chặn 403 thật (server-side), không chỉ ẩn ở UI.
    [Fact]
    public async Task Teacher_A_cannot_view_stats_of_lop_managed_by_teacher_B()
    {
        var (_, lopId) = SeedKhoaAndLop("K-TeacherB", "Lop-TeacherB");
        var teacherBId = Guid.NewGuid();
        _factory.AuthClient.Lops[lopId] = _factory.AuthClient.Lops[lopId] with { GiaoVienId = teacherBId };
        SeedStudentInLop(lopId, "SV-B1");

        // teacherAToken hợp lệ (Role=Teacher) nhưng KHÔNG phải GiaoVienId của lopId ở trên.
        var teacherAId = Guid.NewGuid();
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/lop/{lopId}", TestTokens.Teacher(teacherAId)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_with_no_lop_assigned_cannot_view_any_lop_stats()
    {
        var (_, lopId) = SeedKhoaAndLop("K-Unassigned", "Lop-Unassigned"); // GiaoVienId = null mặc định
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/admin/stats/lop/{lopId}", TestTokens.Teacher()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
