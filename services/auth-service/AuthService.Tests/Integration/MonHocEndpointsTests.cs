using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Api.Dtos;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Xunit;

namespace AuthService.Tests.Integration;

/// <summary>Rà soát Lần XVI (2026-08-21) — CRUD Môn học (panel "Quản lý Môn học"), Admin-only.</summary>
public sealed class MonHocEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public MonHocEndpointsTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage WithAuth(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static readonly JwtTokenService TestTokenService = new(Options.Create(new JwtOptions
    {
        Issuer = "tthcm-platform",
        Audience = "tthcm-services",
        SigningKey = "dev-only-signing-key-do-not-use-in-production-min-32-chars",
    }));

    private static string AdminToken(out Guid adminId)
    {
        adminId = Guid.NewGuid();
        return TestTokenService.IssueAccessToken(adminId.ToString(), "admin@test.local", "Admin Test", Roles.Admin).AccessToken;
    }

    private static string TeacherToken(out Guid teacherId)
    {
        teacherId = Guid.NewGuid();
        return TestTokenService.IssueAccessToken(teacherId.ToString(), $"teacher-{teacherId:N}@test.local", "Teacher Test", Roles.Teacher).AccessToken;
    }

    private async Task<Guid> RegisterTeacherAsync(string namePrefix, string adminToken)
    {
        var email = $"{namePrefix}-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", namePrefix));
        var studentId = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!.User.Id;

        var promoteRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/role", adminToken);
        promoteRequest.Headers.Add("X-Internal-Key", AuthApiFactory.TestInternalServiceKey);
        promoteRequest.Content = JsonContent.Create(new ChangeRoleRequest(Roles.Teacher));
        await _client.SendAsync(promoteRequest);
        return studentId;
    }

    private async Task<Guid> CreateKhoaAsync(string ten, string adminToken)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/khoa", adminToken);
        request.Content = JsonContent.Create(new CreateKhoaRequest(ten));
        var response = await _client.SendAsync(request);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<KhoaResponse>>())!.Data!;
        return body.Id;
    }

    private async Task<Guid> CreateLopAsync(string ten, Guid khoaId, string adminToken)
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", adminToken);
        request.Content = JsonContent.Create(new CreateLopRequest(ten, khoaId));
        var response = await _client.SendAsync(request);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!;
        return body.Id;
    }

    [Fact]
    public async Task Admin_can_create_and_list_mon_hoc()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("monhoc-create", adminToken);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/mon-hoc", adminToken);
        createRequest.Content = JsonContent.Create(new CreateMonHocRequest("Học phần Test", "TEST101", 3, teacherId));
        var createResponse = await _client.SendAsync(createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<MonHocResponse>>())!.Data!;
        Assert.Equal("Học phần Test", created.Ten);
        Assert.Equal("TEST101", created.MaHocPhan);
        Assert.Equal(3, created.TinChi);
        Assert.Equal(teacherId, created.GiaoVienId);
        Assert.Empty(created.LopDangHoc);

        var listResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/auth/mon-hoc", adminToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<MonHocResponse>>>())!.Data!;
        Assert.Contains(list, m => m.Id == created.Id);
    }

    [Fact]
    public async Task Creating_mon_hoc_with_unknown_teacher_returns_404()
    {
        var adminToken = AdminToken(out _);
        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/mon-hoc", adminToken);
        request.Content = JsonContent.Create(new CreateMonHocRequest("Kinh tế chính trị", "KTCT101", 2, Guid.NewGuid()));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Creating_mon_hoc_with_out_of_range_tin_chi_is_rejected()
    {
        var adminToken = AdminToken(out _);
        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/mon-hoc", adminToken);
        request.Content = JsonContent.Create(new CreateMonHocRequest("Môn không hợp lệ", "MKHL101", 0, null));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_mon_hoc_with_empty_ma_hoc_phan_is_rejected()
    {
        var adminToken = AdminToken(out _);
        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/mon-hoc", adminToken);
        request.Content = JsonContent.Create(new CreateMonHocRequest("Môn thiếu mã", "", 2, null));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_cannot_create_mon_hoc()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/mon-hoc", TeacherToken(out _));
        request.Content = JsonContent.Create(new CreateMonHocRequest("Không được tạo", "KDT101", 2, null));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_update_mon_hoc()
    {
        var adminToken = AdminToken(out _);
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/mon-hoc", adminToken);
        createRequest.Content = JsonContent.Create(new CreateMonHocRequest("Tên cũ", "MA-CU", 2, null));
        var created = (await (await _client.SendAsync(createRequest)).Content.ReadFromJsonAsync<ApiResponse<MonHocResponse>>())!.Data!;

        var updateRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/mon-hoc/{created.Id}", adminToken);
        updateRequest.Content = JsonContent.Create(new UpdateMonHocRequest("Tên mới", "MA-MOI", 4, null));
        var updateResponse = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<ApiResponse<MonHocResponse>>())!.Data!;
        Assert.Equal("Tên mới", updated.Ten);
        Assert.Equal("MA-MOI", updated.MaHocPhan);
        Assert.Equal(4, updated.TinChi);
    }

    [Fact]
    public async Task Admin_can_delete_mon_hoc()
    {
        var adminToken = AdminToken(out _);
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/mon-hoc", adminToken);
        createRequest.Content = JsonContent.Create(new CreateMonHocRequest("Sẽ bị xóa", "SBX101", 2, null));
        var created = (await (await _client.SendAsync(createRequest)).Content.ReadFromJsonAsync<ApiResponse<MonHocResponse>>())!.Data!;

        var deleteResponse = await _client.SendAsync(WithAuth(HttpMethod.Delete, $"/api/v1/auth/mon-hoc/{created.Id}", adminToken));
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var listResponse = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/auth/mon-hoc", adminToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<MonHocResponse>>>())!.Data!;
        Assert.DoesNotContain(list, m => m.Id == created.Id);
    }

    [Fact]
    public async Task Admin_can_assign_lop_list_to_mon_hoc_and_it_persists()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lop1 = await CreateLopAsync("Lop-MonHoc-1", khoaId, adminToken);
        var lop2 = await CreateLopAsync("Lop-MonHoc-2", khoaId, adminToken);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/mon-hoc", adminToken);
        createRequest.Content = JsonContent.Create(new CreateMonHocRequest("Lịch sử Đảng", "LSD101", 3, null));
        var created = (await (await _client.SendAsync(createRequest)).Content.ReadFromJsonAsync<ApiResponse<MonHocResponse>>())!.Data!;

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/mon-hoc/{created.Id}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignMonHocLopRequest([lop1, lop2]));
        var assignResponse = await _client.SendAsync(assignRequest);

        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        var assigned = (await assignResponse.Content.ReadFromJsonAsync<ApiResponse<MonHocResponse>>())!.Data!;
        Assert.Equal(2, assigned.LopDangHoc.Count);
        Assert.Contains(assigned.LopDangHoc, l => l.LopId == lop1);
        Assert.Contains(assigned.LopDangHoc, l => l.LopId == lop2);

        // Gán lại với danh sách khác phải GHI ĐÈ, không cộng dồn.
        var reassignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/mon-hoc/{created.Id}/lop", adminToken);
        reassignRequest.Content = JsonContent.Create(new AssignMonHocLopRequest([lop1]));
        var reassignResponse = await _client.SendAsync(reassignRequest);
        var reassigned = (await reassignResponse.Content.ReadFromJsonAsync<ApiResponse<MonHocResponse>>())!.Data!;
        Assert.Single(reassigned.LopDangHoc);
        Assert.Equal(lop1, reassigned.LopDangHoc[0].LopId);
    }

    [Fact]
    public async Task Assigning_an_unknown_lop_to_mon_hoc_returns_404()
    {
        var adminToken = AdminToken(out _);
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/mon-hoc", adminToken);
        createRequest.Content = JsonContent.Create(new CreateMonHocRequest("Môn test lop lạ", "MTLL101", 2, null));
        var created = (await (await _client.SendAsync(createRequest)).Content.ReadFromJsonAsync<ApiResponse<MonHocResponse>>())!.Data!;

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/mon-hoc/{created.Id}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignMonHocLopRequest([Guid.NewGuid()]));
        var response = await _client.SendAsync(assignRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_list_mon_hoc()
    {
        var email = $"student-monhoc-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", "Học viên"));
        var studentToken = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!.AccessToken;

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/auth/mon-hoc", studentToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
