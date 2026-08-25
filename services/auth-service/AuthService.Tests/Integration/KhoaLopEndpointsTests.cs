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

public sealed class KhoaLopEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public KhoaLopEndpointsTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage WithAuth(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    // GET /users (kể cả filter lopId/khoaId mới ở Bước C) yêu cầu thêm X-Internal-Key — endpoint
    // này có từ trước Bước C, xem RequireInternalServiceKeyFilter remarks trong AuthEndpoints.cs.
    private static HttpRequestMessage WithAuthAndInternalKey(HttpMethod method, string url, string token)
    {
        var request = WithAuth(method, url, token);
        request.Headers.Add("X-Internal-Key", AuthApiFactory.TestInternalServiceKey);
        return request;
    }

    // Must match appsettings.Development.json, which the test WebApplicationFactory loads.
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

    private async Task<Guid> RegisterStudentAsync(string namePrefix)
    {
        var email = $"{namePrefix}-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", namePrefix));
        var student = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;
        return student.User.Id;
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

    // ---------------------------------------------------------------------------
    // Khóa CRUD
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_create_and_list_khoa()
    {
        var adminToken = AdminToken(out _);
        var ten = $"K{Guid.NewGuid():N}"[..8];

        var khoaId = await CreateKhoaAsync(ten, adminToken);

        var listRequest = WithAuth(HttpMethod.Get, "/api/v1/auth/khoa", adminToken);
        var listResponse = await _client.SendAsync(listRequest);
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<KhoaResponse>>>())!.Data!;

        Assert.Contains(list, k => k.Id == khoaId && k.Ten == ten);
    }

    // Việc 4.2 (2026-08-19) — Teacher cần đọc danh sách Khóa để chọn khi tạo Lớp mới.
    [Fact]
    public async Task Teacher_can_list_khoa()
    {
        var adminToken = AdminToken(out _);
        var ten = $"K{Guid.NewGuid():N}"[..8];
        var khoaId = await CreateKhoaAsync(ten, adminToken);
        var teacherToken = TeacherToken(out _);

        var listRequest = WithAuth(HttpMethod.Get, "/api/v1/auth/khoa", teacherToken);
        var listResponse = await _client.SendAsync(listRequest);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<KhoaResponse>>>())!.Data!;
        Assert.Contains(list, k => k.Id == khoaId);
    }

    [Fact]
    public async Task Admin_can_get_khoa_by_id()
    {
        var adminToken = AdminToken(out _);
        var ten = $"K{Guid.NewGuid():N}"[..8];
        var khoaId = await CreateKhoaAsync(ten, adminToken);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/khoa/{khoaId}", adminToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<KhoaResponse>>())!.Data!;
        Assert.Equal(khoaId, body.Id);
        Assert.Equal(ten, body.Ten);
    }

    [Fact]
    public async Task Get_unknown_khoa_by_id_returns_not_found()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/khoa/{Guid.NewGuid()}", AdminToken(out _)));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Khác với List/Create/Update/Delete (Admin-only): GET khoa/{id} và lop/{id} đơn lẻ mở cho
    // bất kỳ user đã đăng nhập nào — học viên cần tự resolve tên Lớp/Khóa của mình từ LopId trả
    // về ở /me để hiển thị hồ sơ (Gap 1 fix).
    [Fact]
    public async Task Student_can_read_khoa_by_id()
    {
        var adminToken = AdminToken(out _);
        var ten = $"K{Guid.NewGuid():N}"[..8];
        var khoaId = await CreateKhoaAsync(ten, adminToken);

        var studentId = await RegisterStudentAsync("read-khoa-student");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), "x@test.local", "X", Roles.Student).AccessToken;
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/khoa/{khoaId}", studentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<KhoaResponse>>())!.Data!;
        Assert.Equal(ten, body.Ten);
    }

    [Fact]
    public async Task Student_can_read_lop_by_id()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);

        var studentId = await RegisterStudentAsync("read-lop-student");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), "y@test.local", "Y", Roles.Student).AccessToken;
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}", studentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!;
        Assert.Equal("CNTT1", body.Ten);
    }

    [Fact]
    public async Task Unauthenticated_request_still_rejected_for_khoa_by_id()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);

        var response = await _client.GetAsync($"/api/v1/auth/khoa/{khoaId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_get_lop_by_id()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}", adminToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!;
        Assert.Equal(lopId, body.Id);
        Assert.Equal(khoaId, body.KhoaId);
    }

    [Fact]
    public async Task Get_unknown_lop_by_id_returns_not_found()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{Guid.NewGuid()}", AdminToken(out _)));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Users_list_can_filter_by_lop_and_khoa()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var inLopStudent = await RegisterStudentAsync("filter-in-lop");
        var outsideStudent = await RegisterStudentAsync("filter-outside-lop");

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{inLopStudent}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignRequest);

        var byLopResponse = await _client.SendAsync(WithAuthAndInternalKey(HttpMethod.Get, $"/api/v1/auth/users?lopId={lopId}", adminToken));
        var byLopList = (await byLopResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserResponse>>>())!.Data!;
        Assert.Contains(byLopList, u => u.Id == inLopStudent);
        Assert.DoesNotContain(byLopList, u => u.Id == outsideStudent);

        var byKhoaResponse = await _client.SendAsync(WithAuthAndInternalKey(HttpMethod.Get, $"/api/v1/auth/users?khoaId={khoaId}", adminToken));
        var byKhoaList = (await byKhoaResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserResponse>>>())!.Data!;
        Assert.Contains(byKhoaList, u => u.Id == inLopStudent);
        Assert.DoesNotContain(byKhoaList, u => u.Id == outsideStudent);
    }

    // Việc C (2026-08-16) — roster tối thiểu (Id/Name, không Email) cho bảng xếp hạng theo Lớp,
    // service-to-service (quiz-service gọi). RequireAuthorization() thường + internal key, KHÔNG
    // RequireRole — endpoint tin caller (quiz-service) đã tự kiểm ownership trước khi hỏi, nên ở
    // đây chỉ cần verify: có JWT hợp lệ + đúng internal key thì qua, thiếu 1 trong 2 đều bị chặn.
    [Fact]
    public async Task Hoc_vien_ids_returns_only_students_with_minimal_fields()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var inLopStudent = await RegisterStudentAsync("hocvienids-in");
        var outsideStudent = await RegisterStudentAsync("hocvienids-outside");

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{inLopStudent}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignRequest);

        var response = await _client.SendAsync(WithAuthAndInternalKey(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/hoc-vien-ids", adminToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<List<HocVienIdResponse>>>())!.Data!;
        Assert.Contains(body, u => u.Id == inLopStudent);
        Assert.DoesNotContain(body, u => u.Id == outsideStudent);
    }

    [Fact]
    public async Task Hoc_vien_ids_is_callable_with_a_student_jwt_as_long_as_internal_key_is_present()
    {
        // Endpoint không RequireRole — biên bảo mật thật là internal key, không phải role, vì
        // quiz-service forward nguyên JWT của người gọi gốc (có thể là Student xem lớp mình).
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("hocvienids-caller");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), $"caller-{studentId:N}@test.local", "Caller Test", Roles.Student).AccessToken;

        var response = await _client.SendAsync(WithAuthAndInternalKey(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/hoc-vien-ids", studentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Hoc_vien_ids_without_internal_key_is_rejected_even_with_valid_admin_jwt()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/hoc-vien-ids", adminToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Hoc_vien_ids_for_nonexistent_lop_returns_404()
    {
        var adminToken = AdminToken(out _);
        var response = await _client.SendAsync(WithAuthAndInternalKey(HttpMethod.Get, $"/api/v1/auth/lop/{Guid.NewGuid()}/hoc-vien-ids", adminToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_create_khoa()
    {
        var studentId = await RegisterStudentAsync("khoa-forbidden");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), "x@test.local", "X", Roles.Student).AccessToken;

        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/khoa", studentToken);
        request.Content = JsonContent.Create(new CreateKhoaRequest("K99"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cannot_delete_khoa_that_still_has_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        await CreateLopAsync("CNTT1", khoaId, adminToken);

        var deleteRequest = WithAuth(HttpMethod.Delete, $"/api/v1/auth/khoa/{khoaId}", adminToken);
        var response = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Lớp CRUD
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Create_lop_with_unknown_khoa_returns_not_found()
    {
        var adminToken = AdminToken(out _);
        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", adminToken);
        request.Content = JsonContent.Create(new CreateLopRequest("CNTT1", Guid.NewGuid()));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Lop_list_can_filter_by_khoa()
    {
        var adminToken = AdminToken(out _);
        var khoaA = await CreateKhoaAsync($"KA{Guid.NewGuid():N}"[..8], adminToken);
        var khoaB = await CreateKhoaAsync($"KB{Guid.NewGuid():N}"[..8], adminToken);
        var lopA = await CreateLopAsync("LopA", khoaA, adminToken);
        await CreateLopAsync("LopB", khoaB, adminToken);

        var request = WithAuth(HttpMethod.Get, $"/api/v1/auth/lop?khoaId={khoaA}", adminToken);
        var response = await _client.SendAsync(request);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<LopResponse>>>())!.Data!;

        Assert.Single(list);
        Assert.Equal(lopA, list[0].Id);
    }

    [Fact]
    public async Task Cannot_delete_lop_that_still_has_students()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("lop-student");

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignRequest);

        var deleteRequest = WithAuth(HttpMethod.Delete, $"/api/v1/auth/lop/{lopId}", adminToken);
        var response = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Việc 4.2 (2026-08-19) — Teacher Lớp CRUD (trước Admin-only)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Teacher_creating_a_lop_automatically_becomes_chu_nhiem()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var teacherToken = TeacherToken(out var teacherId);

        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", teacherToken);
        request.Content = JsonContent.Create(new CreateLopRequest("Lớp GV tự tạo", khoaId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!;
        Assert.Equal(teacherId, body.GiaoVienId);
    }

    [Fact]
    public async Task Admin_creating_a_lop_leaves_giao_vien_empty_as_before()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);

        var lopId = await CreateLopAsync("Lớp Admin tạo", khoaId, adminToken);

        var getRequest = WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}", adminToken);
        var getResponse = await _client.SendAsync(getRequest);
        var body = (await getResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!;
        Assert.Null(body.GiaoVienId);
    }

    [Fact]
    public async Task Student_cannot_create_lop()
    {
        var studentId = await RegisterStudentAsync("lop-create-forbidden");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), "x2@test.local", "X2", Roles.Student).AccessToken;
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);

        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", studentToken);
        request.Content = JsonContent.Create(new CreateLopRequest("Không được tạo", khoaId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Chu_nhiem_can_rename_their_own_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var teacherToken = TeacherToken(out var teacherId);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", teacherToken);
        createRequest.Content = JsonContent.Create(new CreateLopRequest("Tên cũ", khoaId));
        var createResponse = await _client.SendAsync(createRequest);
        var lopId = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!.Id;

        var renameRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}", teacherToken);
        renameRequest.Content = JsonContent.Create(new UpdateLopRequest("Tên mới"));
        var renameResponse = await _client.SendAsync(renameRequest);

        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);
        var body = (await renameResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!;
        Assert.Equal("Tên mới", body.Ten);
    }

    // Rà soát Lần IX (2026-08-21) — thêm KhoaId vào UpdateLopRequest (trước đây modal "Sửa lớp"
    // thiếu hẳn field Khóa, không có cách nào chuyển 1 Lớp sang Khóa khác sau khi tạo).
    [Fact]
    public async Task Chu_nhiem_can_move_their_own_lop_to_another_khoa()
    {
        var adminToken = AdminToken(out _);
        var khoaA = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var khoaB = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var teacherToken = TeacherToken(out _);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", teacherToken);
        createRequest.Content = JsonContent.Create(new CreateLopRequest("Lớp đổi khóa", khoaA));
        var createResponse = await _client.SendAsync(createRequest);
        var lopId = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!.Id;

        var updateRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}", teacherToken);
        updateRequest.Content = JsonContent.Create(new UpdateLopRequest("Lớp đổi khóa", KhoaId: khoaB));
        var updateResponse = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var body = (await updateResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!;
        Assert.Equal(khoaB, body.KhoaId);
    }

    [Fact]
    public async Task Updating_a_lop_with_unknown_khoa_id_returns_404()
    {
        var adminToken = AdminToken(out _);
        var khoaA = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var teacherToken = TeacherToken(out _);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", teacherToken);
        createRequest.Content = JsonContent.Create(new CreateLopRequest("Lớp khóa không tồn tại", khoaA));
        var createResponse = await _client.SendAsync(createRequest);
        var lopId = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!.Id;

        var updateRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}", teacherToken);
        updateRequest.Content = JsonContent.Create(new UpdateLopRequest("Lớp khóa không tồn tại", KhoaId: Guid.NewGuid()));
        var updateResponse = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Teacher_A_cannot_rename_lop_managed_by_teacher_B()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var teacherAToken = TeacherToken(out _);
        var teacherBToken = TeacherToken(out _);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", teacherAToken);
        createRequest.Content = JsonContent.Create(new CreateLopRequest("Lớp của GV A", khoaId));
        var createResponse = await _client.SendAsync(createRequest);
        var lopId = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!.Id;

        var renameRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}", teacherBToken);
        renameRequest.Content = JsonContent.Create(new UpdateLopRequest("GV B cố đổi tên"));
        var response = await _client.SendAsync(renameRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_rename_any_lop_regardless_of_chu_nhiem()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var teacherToken = TeacherToken(out _);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", teacherToken);
        createRequest.Content = JsonContent.Create(new CreateLopRequest("Lớp của GV", khoaId));
        var createResponse = await _client.SendAsync(createRequest);
        var lopId = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!.Id;

        var renameRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}", adminToken);
        renameRequest.Content = JsonContent.Create(new UpdateLopRequest("Admin đổi tên"));
        var response = await _client.SendAsync(renameRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Chu_nhiem_can_delete_their_own_empty_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var teacherToken = TeacherToken(out _);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", teacherToken);
        createRequest.Content = JsonContent.Create(new CreateLopRequest("Lớp rỗng của GV", khoaId));
        var createResponse = await _client.SendAsync(createRequest);
        var lopId = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!.Id;

        var deleteRequest = WithAuth(HttpMethod.Delete, $"/api/v1/auth/lop/{lopId}", teacherToken);
        var response = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_A_cannot_delete_lop_managed_by_teacher_B()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var teacherAToken = TeacherToken(out _);
        var teacherBToken = TeacherToken(out _);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", teacherAToken);
        createRequest.Content = JsonContent.Create(new CreateLopRequest("Lớp của GV A (không được xóa bởi B)", khoaId));
        var createResponse = await _client.SendAsync(createRequest);
        var lopId = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!.Id;

        var deleteRequest = WithAuth(HttpMethod.Delete, $"/api/v1/auth/lop/{lopId}", teacherBToken);
        var response = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Chu_nhiem_cannot_delete_their_own_lop_if_it_still_has_students()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var teacherToken = TeacherToken(out _);

        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/auth/lop", teacherToken);
        createRequest.Content = JsonContent.Create(new CreateLopRequest("Lớp có học viên", khoaId));
        var createResponse = await _client.SendAsync(createRequest);
        var lopId = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!.Id;

        var studentId = await RegisterStudentAsync("chu-nhiem-delete-guard");
        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", teacherToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignRequest);

        var deleteRequest = WithAuth(HttpMethod.Delete, $"/api/v1/auth/lop/{lopId}", teacherToken);
        var response = await _client.SendAsync(deleteRequest);

        // Guard "còn học viên" vẫn áp dụng cho Teacher y hệt Admin — quyền sở hữu không bỏ qua
        // được ràng buộc an toàn dữ liệu này.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Gán học viên vào Lớp
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_assign_student_to_lop_and_it_persists()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("assign-student");

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        var response = await _client.SendAsync(assignRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(lopId, body.LopId);

        // Gỡ khỏi lớp — LopId = null.
        var unassignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        unassignRequest.Content = JsonContent.Create(new AssignLopRequest(null));
        var unassignResponse = await _client.SendAsync(unassignRequest);
        var unassignBody = (await unassignResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Null(unassignBody.LopId);
    }

    [Fact]
    public async Task Assign_student_to_unknown_lop_returns_not_found()
    {
        var adminToken = AdminToken(out _);
        var studentId = await RegisterStudentAsync("assign-unknown-lop");

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        request.Content = JsonContent.Create(new AssignLopRequest(Guid.NewGuid()));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Gán giáo viên chủ nhiệm
    // ---------------------------------------------------------------------------

    /// <summary>Đăng ký 1 user Student thật rồi promote lên Teacher qua endpoint role-change có
    /// sẵn (cần X-Internal-Key, mô phỏng đúng cách admin-service gọi) — /register luôn tạo Student,
    /// không có cách nào tạo thẳng user Role=Teacher qua HTTP public.</summary>
    private async Task<Guid> RegisterTeacherAsync(string namePrefix, string adminToken)
    {
        var teacherId = await RegisterStudentAsync(namePrefix);
        var promoteRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/role", adminToken);
        promoteRequest.Headers.Add("X-Internal-Key", AuthApiFactory.TestInternalServiceKey);
        promoteRequest.Content = JsonContent.Create(new ChangeRoleRequest(Roles.Teacher));
        await _client.SendAsync(promoteRequest);
        return teacherId;
    }

    [Fact]
    public async Task Admin_can_assign_teacher_as_giao_vien_and_it_persists()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var teacherId = await RegisterTeacherAsync("giao-vien", adminToken);

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherId));
        var response = await _client.SendAsync(assignRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!;
        Assert.Equal(teacherId, body.GiaoVienId);

        // Gỡ giáo viên — GiaoVienId = null.
        var unassignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        unassignRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(null));
        var unassignResponse = await _client.SendAsync(unassignRequest);
        var unassignBody = (await unassignResponse.Content.ReadFromJsonAsync<ApiResponse<LopResponse>>())!.Data!;
        Assert.Null(unassignBody.GiaoVienId);
    }

    [Fact]
    public async Task Assign_giao_vien_rejects_non_teacher_user()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("not-a-teacher");

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        request.Content = JsonContent.Create(new AssignGiaoVienRequest(studentId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Chức vụ — Admin, GV chủ nhiệm đúng lớp, GV không chủ nhiệm, Student
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_change_chuc_vu_of_any_student()
    {
        var adminToken = AdminToken(out _);
        var studentId = await RegisterStudentAsync("chucvu-admin");

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/chuc-vu", adminToken);
        request.Content = JsonContent.Create(new ChangeChucVuRequest(ChucVuValuesForTest.LopTruong));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(ChucVuValuesForTest.LopTruong, body.ChucVu);
    }

    [Fact]
    public async Task Change_chuc_vu_rejects_unknown_value()
    {
        var adminToken = AdminToken(out _);
        var studentId = await RegisterStudentAsync("chucvu-badvalue");

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/chuc-vu", adminToken);
        request.Content = JsonContent.Create(new ChangeChucVuRequest("Bí thư"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_change_chuc_vu()
    {
        var targetId = await RegisterStudentAsync("chucvu-target");
        var callerId = await RegisterStudentAsync("chucvu-caller");
        var callerToken = TestTokenService.IssueAccessToken(callerId.ToString(), "caller@test.local", "Caller", Roles.Student).AccessToken;

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{targetId}/chuc-vu", callerToken);
        request.Content = JsonContent.Create(new ChangeChucVuRequest(ChucVuValuesForTest.LopTruong));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_assigned_as_chu_nhiem_can_change_chuc_vu_of_their_students()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var teacherId = await RegisterTeacherAsync("chu-nhiem", adminToken);
        var studentId = await RegisterStudentAsync("chucvu-under-chu-nhiem");

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherId));
        await _client.SendAsync(assignGvRequest);

        var assignLopRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignLopRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignLopRequest);

        // JWT khớp đúng teacherId thật đã được gán làm GiaoVienId ở trên.
        var teacherToken = TestTokenService.IssueAccessToken(teacherId.ToString(), "cn@test.local", "Chủ nhiệm", Roles.Teacher).AccessToken;
        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/chuc-vu", teacherToken);
        request.Content = JsonContent.Create(new ChangeChucVuRequest(ChucVuValuesForTest.LopTruong));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(ChucVuValuesForTest.LopTruong, body.ChucVu);
    }

    [Fact]
    public async Task Teacher_not_assigned_as_chu_nhiem_cannot_change_chuc_vu()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("chucvu-unrelated-teacher");

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignRequest);

        // teacherToken hợp lệ (Role=Teacher) nhưng KHÔNG phải GiaoVienId của lopId ở trên.
        var teacherToken = TeacherToken(out _);
        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/chuc-vu", teacherToken);
        request.Content = JsonContent.Create(new ChangeChucVuRequest(ChucVuValuesForTest.LopTruong));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Việc V (2026-08-20) — Cấp bậc, cùng pattern RBAC với Chức vụ ở trên
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_change_cap_bac_of_any_student()
    {
        var adminToken = AdminToken(out _);
        var studentId = await RegisterStudentAsync("capbac-admin");

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/cap-bac", adminToken);
        request.Content = JsonContent.Create(new ChangeCapBacRequest(CapBacValues.All[0]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(CapBacValues.All[0], body.CapBac);
    }

    [Fact]
    public async Task Change_cap_bac_rejects_unknown_value()
    {
        var adminToken = AdminToken(out _);
        var studentId = await RegisterStudentAsync("capbac-badvalue");

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/cap-bac", adminToken);
        request.Content = JsonContent.Create(new ChangeCapBacRequest("Không tồn tại"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_change_cap_bac()
    {
        var targetId = await RegisterStudentAsync("capbac-target");
        var callerId = await RegisterStudentAsync("capbac-caller");
        var callerToken = TestTokenService.IssueAccessToken(callerId.ToString(), "caller2@test.local", "Caller", Roles.Student).AccessToken;

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{targetId}/cap-bac", callerToken);
        request.Content = JsonContent.Create(new ChangeCapBacRequest(CapBacValues.All[0]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_assigned_as_chu_nhiem_can_change_cap_bac_of_their_students()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var teacherId = await RegisterTeacherAsync("chu-nhiem-capbac", adminToken);
        var studentId = await RegisterStudentAsync("capbac-under-chu-nhiem");

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherId));
        await _client.SendAsync(assignGvRequest);

        var assignLopRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignLopRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignLopRequest);

        var teacherToken = TestTokenService.IssueAccessToken(teacherId.ToString(), "cn-capbac@test.local", "Chủ nhiệm", Roles.Teacher).AccessToken;
        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/cap-bac", teacherToken);
        request.Content = JsonContent.Create(new ChangeCapBacRequest(CapBacValues.All[0]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(CapBacValues.All[0], body.CapBac);
    }

    [Fact]
    public async Task Teacher_not_assigned_as_chu_nhiem_cannot_change_cap_bac()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("capbac-unrelated-teacher");

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignRequest);

        var teacherToken = TeacherToken(out _);
        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/cap-bac", teacherToken);
        request.Content = JsonContent.Create(new ChangeCapBacRequest(CapBacValues.All[0]));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Rà soát Lần VI (2026-08-21) — Môn học phụ trách, CHỈ Admin sửa được (khác Cấp bậc/Chức vụ ở
    // trên cho cả Teacher chủ nhiệm — Môn học phụ trách không có khái niệm "chủ nhiệm").
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_set_mon_hoc_phu_trach_of_teacher()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("monhoc-admin", adminToken);

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/mon-hoc-phu-trach", adminToken);
        request.Content = JsonContent.Create(new ChangeMonHocPhuTrachRequest("Học phần Test"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal("Học phần Test", body.MonHocPhuTrach);
    }

    [Fact]
    public async Task Teacher_cannot_set_mon_hoc_phu_trach_even_of_self()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("monhoc-teacher-self", adminToken);
        var teacherToken = TestTokenService.IssueAccessToken(teacherId.ToString(), "mh-self@test.local", "GV", Roles.Teacher).AccessToken;

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/mon-hoc-phu-trach", teacherToken);
        request.Content = JsonContent.Create(new ChangeMonHocPhuTrachRequest("Học phần Test"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Rà soát Lần XIV (2026-08-21) — panel "Quản lý GV": Admin sửa Chức vụ chuyên môn của GV khác
    // trực tiếp (trước đây CHỈ GV tự sửa qua Hồ sơ cá nhân, không có đường Admin can thiệp).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_set_chuc_vu_gv_of_teacher()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("chucvu-admin", adminToken);

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/chuc-vu-gv", adminToken);
        request.Content = JsonContent.Create(new ChangeChucVuGvRequest(ChucVuGvValues.GiangVienChinh));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(ChucVuGvValues.GiangVienChinh, body.ChucVuGV);
    }

    [Fact]
    public async Task Teacher_cannot_set_chuc_vu_gv_of_another_teacher()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("chucvu-teacher-other", adminToken);
        var otherTeacherToken = TeacherToken(out _);

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/chuc-vu-gv", otherTeacherToken);
        request.Content = JsonContent.Create(new ChangeChucVuGvRequest(ChucVuGvValues.TruongBoMon));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Setting_an_invalid_chuc_vu_gv_value_is_rejected()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("chucvu-invalid", adminToken);

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/chuc-vu-gv", adminToken);
        request.Content = JsonContent.Create(new ChangeChucVuGvRequest("Chủ nhiệm khoa gì đó không hợp lệ"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Rà soát Lần XVIII (2026-08-22) — Admin sửa Họ tên + Năm học của người khác (panel Quản lý
    // GV/Quản lý Tài khoản) — trước đây KHÔNG có đường nào ngoài tự sửa (PUT /me).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_edit_name_and_nam_hoc_of_another_user()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("adminedit-name", adminToken);

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/admin-edit", adminToken);
        request.Content = JsonContent.Create(new AdminEditUserRequest("Tên Đã Sửa Bởi Admin", "2025-2026"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal("Tên Đã Sửa Bởi Admin", body.Name);
        Assert.Equal("2025-2026", body.NamHoc);
    }

    [Fact]
    public async Task Teacher_cannot_admin_edit_another_users_name()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("adminedit-forbidden", adminToken);
        var otherTeacherToken = TeacherToken(out _);

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/admin-edit", otherTeacherToken);
        request.Content = JsonContent.Create(new AdminEditUserRequest("Không được sửa", null));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_edit_with_empty_name_is_rejected()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("adminedit-empty", adminToken);

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/admin-edit", adminToken);
        request.Content = JsonContent.Create(new AdminEditUserRequest("", null));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_edit_with_invalid_nam_hoc_format_is_rejected()
    {
        var adminToken = AdminToken(out _);
        var teacherId = await RegisterTeacherAsync("adminedit-badnamhoc", adminToken);

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{teacherId}/admin-edit", adminToken);
        request.Content = JsonContent.Create(new AdminEditUserRequest("Tên hợp lệ", "nam-hoc-sai-dinh-dang"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Gap 2 — GET /lop/mine (Teacher tự lấy lớp mình phụ trách)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Teacher_lop_mine_returns_only_their_own_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var myLopId = await CreateLopAsync("Lop-Cua-Toi", khoaId, adminToken);
        var otherLopId = await CreateLopAsync("Lop-Nguoi-Khac", khoaId, adminToken);
        var teacherId = await RegisterTeacherAsync("mine-teacher", adminToken);

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{myLopId}/giao-vien", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherId));
        await _client.SendAsync(assignRequest);
        // otherLopId cố ý không gán GV nào — kiểm tra "mine" không lẫn lớp chưa có chủ nhiệm.
        _ = otherLopId;

        var teacherToken = TestTokenService.IssueAccessToken(teacherId.ToString(), "mine@test.local", "Mine", Roles.Teacher).AccessToken;
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/auth/lop/mine", teacherToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<LopResponse>>>())!.Data!;
        Assert.Single(list);
        Assert.Equal(myLopId, list[0].Id);
    }

    [Fact]
    public async Task Teacher_with_no_lop_gets_empty_list_not_error()
    {
        var teacherToken = TeacherToken(out _);
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/auth/lop/mine", teacherToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<LopResponse>>>())!.Data!;
        Assert.Empty(list);
    }

    [Fact]
    public async Task Student_cannot_call_lop_mine()
    {
        var studentId = await RegisterStudentAsync("mine-forbidden");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), "x@test.local", "X", Roles.Student).AccessToken;

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/auth/lop/mine", studentToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Gap 2 — GET /lop/{id}/hoc-vien (roster)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_list_hoc_vien_of_any_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("hocvien-admin");

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignRequest);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/hoc-vien", adminToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<UserResponse>>>())!.Data!;
        Assert.Contains(list, u => u.Id == studentId);
    }

    [Fact]
    public async Task Chu_nhiem_can_list_hoc_vien_of_their_own_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var teacherId = await RegisterTeacherAsync("hocvien-chunhiem", adminToken);
        var studentId = await RegisterStudentAsync("hocvien-under-chunhiem");

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherId));
        await _client.SendAsync(assignGvRequest);

        var assignLopRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignLopRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignLopRequest);

        var teacherToken = TestTokenService.IssueAccessToken(teacherId.ToString(), "cn2@test.local", "Chủ nhiệm 2", Roles.Teacher).AccessToken;
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/hoc-vien", teacherToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<UserResponse>>>())!.Data!;
        Assert.Contains(list, u => u.Id == studentId);
    }

    // Trọng tâm test RBAC 2 chiều theo yêu cầu: GV A (không phải chủ nhiệm lopId này) gọi
    // /hoc-vien của lớp GV B phụ trách phải bị chặn 403 — không chỉ ẩn ở UI.
    [Fact]
    public async Task Teacher_A_cannot_list_hoc_vien_of_lop_managed_by_teacher_B()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var teacherBId = await RegisterTeacherAsync("hocvien-teacher-b", adminToken);
        var studentId = await RegisterStudentAsync("hocvien-under-b");

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherBId));
        await _client.SendAsync(assignGvRequest);

        var assignLopRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignLopRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignLopRequest);

        // teacherAToken hợp lệ (Role=Teacher, không bị revoke gì) nhưng KHÔNG phải GiaoVienId của lopId.
        var teacherAToken = TeacherToken(out _);
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/hoc-vien", teacherAToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_list_hoc_vien()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("hocvien-student-forbidden");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), "z@test.local", "Z", Roles.Student).AccessToken;

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/hoc-vien", studentToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_hoc_vien_of_unknown_lop_returns_not_found()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{Guid.NewGuid()}/hoc-vien", AdminToken(out _)));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Gap 2 mục 2 — GET /users/search-by-email
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Search_by_email_finds_matching_student()
    {
        var namePrefix = $"search-hit-{Guid.NewGuid():N}"[..20];
        var studentId = await RegisterStudentAsync(namePrefix);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/users/search-by-email?email={namePrefix}", AdminToken(out _)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<StudentSearchResponse>>>())!.Data!;
        Assert.Contains(list, s => s.Id == studentId);
    }

    [Fact]
    public async Task Search_by_email_does_not_return_non_student_roles()
    {
        var adminToken = AdminToken(out _);
        var namePrefix = $"search-teacher-{Guid.NewGuid():N}"[..20];
        var teacherId = await RegisterTeacherAsync(namePrefix, adminToken);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/users/search-by-email?email={namePrefix}", adminToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<StudentSearchResponse>>>())!.Data!;
        Assert.DoesNotContain(list, s => s.Id == teacherId);
    }

    [Fact]
    public async Task Search_by_email_short_query_returns_empty_not_error()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/auth/users/search-by-email?email=ab", AdminToken(out _)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<StudentSearchResponse>>>())!.Data!;
        Assert.Empty(list);
    }

    [Fact]
    public async Task Search_by_email_reflects_current_lop_id()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var namePrefix = $"search-lop-{Guid.NewGuid():N}"[..18];
        var studentId = await RegisterStudentAsync(namePrefix);

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignRequest);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/users/search-by-email?email={namePrefix}", adminToken));
        var list = (await response.Content.ReadFromJsonAsync<ApiResponse<List<StudentSearchResponse>>>())!.Data!;

        Assert.Equal(lopId, list.Single(s => s.Id == studentId).LopId);
    }

    [Fact]
    public async Task Search_by_email_forbidden_for_student()
    {
        var studentId = await RegisterStudentAsync("search-forbidden");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), "x@test.local", "X", Roles.Student).AccessToken;

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, "/api/v1/auth/users/search-by-email?email=abc", studentToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Gap 2 mục 2 — PUT /users/{id}/lop giờ cho phép GV chủ nhiệm (RBAC 2 chiều)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Chu_nhiem_can_assign_student_into_their_own_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var teacherId = await RegisterTeacherAsync("assign-chunhiem", adminToken);
        var studentId = await RegisterStudentAsync("assign-by-chunhiem");

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherId));
        await _client.SendAsync(assignGvRequest);

        var teacherToken = TestTokenService.IssueAccessToken(teacherId.ToString(), "cn3@test.local", "CN3", Roles.Teacher).AccessToken;
        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", teacherToken);
        request.Content = JsonContent.Create(new AssignLopRequest(lopId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(lopId, body.LopId);
    }

    [Fact]
    public async Task Chu_nhiem_can_remove_student_from_their_own_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var teacherId = await RegisterTeacherAsync("remove-chunhiem", adminToken);
        var studentId = await RegisterStudentAsync("remove-by-chunhiem");

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherId));
        await _client.SendAsync(assignGvRequest);
        var assignLopRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignLopRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignLopRequest);

        var teacherToken = TestTokenService.IssueAccessToken(teacherId.ToString(), "cn4@test.local", "CN4", Roles.Teacher).AccessToken;
        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", teacherToken);
        request.Content = JsonContent.Create(new AssignLopRequest(null));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Null(body.LopId);
    }

    // Trọng tâm RBAC 2 chiều theo yêu cầu: Teacher A gán học viên vào lớp Teacher B phụ trách phải
    // bị chặn 403 thật.
    [Fact]
    public async Task Teacher_A_cannot_assign_student_into_lop_managed_by_teacher_B()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopBId = await CreateLopAsync("LopB", khoaId, adminToken);
        var teacherBId = await RegisterTeacherAsync("assign-teacher-b", adminToken);
        var studentId = await RegisterStudentAsync("assign-target-b");

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopBId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherBId));
        await _client.SendAsync(assignGvRequest);

        var teacherAToken = TeacherToken(out _); // không liên quan lopB
        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", teacherAToken);
        request.Content = JsonContent.Create(new AssignLopRequest(lopBId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_A_cannot_remove_student_from_lop_managed_by_teacher_B()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopBId = await CreateLopAsync("LopB2", khoaId, adminToken);
        var teacherBId = await RegisterTeacherAsync("remove-teacher-b", adminToken);
        var studentId = await RegisterStudentAsync("remove-target-b");

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopBId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherBId));
        await _client.SendAsync(assignGvRequest);
        var assignLopRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignLopRequest.Content = JsonContent.Create(new AssignLopRequest(lopBId));
        await _client.SendAsync(assignLopRequest);

        var teacherAToken = TeacherToken(out _); // không liên quan lopB
        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", teacherAToken);
        request.Content = JsonContent.Create(new AssignLopRequest(null));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Đúng thiết kế đã xác nhận: Teacher được kéo học viên "đang thuộc lớp khác" về lớp mình —
    // quyền chỉ xét lớp ĐÍCH, không xét lớp nguồn.
    [Fact]
    public async Task Chu_nhiem_can_move_student_from_another_lop_into_their_own_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopSourceId = await CreateLopAsync("LopSource", khoaId, adminToken);
        var lopDestId = await CreateLopAsync("LopDest", khoaId, adminToken);
        var teacherDestId = await RegisterTeacherAsync("move-teacher-dest", adminToken);
        var studentId = await RegisterStudentAsync("move-student");

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopDestId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherDestId));
        await _client.SendAsync(assignGvRequest);
        var initialAssign = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        initialAssign.Content = JsonContent.Create(new AssignLopRequest(lopSourceId));
        await _client.SendAsync(initialAssign);

        var teacherDestToken = TestTokenService.IssueAccessToken(teacherDestId.ToString(), "dest@test.local", "Dest", Roles.Teacher).AccessToken;
        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", teacherDestToken);
        request.Content = JsonContent.Create(new AssignLopRequest(lopDestId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(lopDestId, body.LopId);
    }

    [Fact]
    public async Task Student_cannot_assign_themselves_to_a_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("self-assign-forbidden");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), "sa@test.local", "SA", Roles.Student).AccessToken;

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", studentToken);
        request.Content = JsonContent.Create(new AssignLopRequest(lopId));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Gap 2 mục 3 — GET /lop/{id}/activity-log
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Chuc_vu_change_writes_activity_log_entry()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("log-chucvu-target");
        var assignLopRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignLopRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignLopRequest);

        var chucVuRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/chuc-vu", adminToken);
        chucVuRequest.Content = JsonContent.Create(new ChangeChucVuRequest(ChucVuValuesForTest.LopTruong));
        await _client.SendAsync(chucVuRequest);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/activity-log", adminToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = (await response.Content.ReadFromJsonAsync<ApiResponse<List<LopActivityLogResponse>>>())!.Data!;

        var entry = Assert.Single(log, l => l.ActionType == "ChucVuChanged" && l.TargetUserId == studentId);
        Assert.Equal("Học viên", entry.OldValue);
        Assert.Equal(ChucVuValuesForTest.LopTruong, entry.NewValue);
    }

    [Fact]
    public async Task Assign_and_remove_lop_write_added_and_removed_log_entries()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("log-assign-target");

        var assignRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        assignRequest.Content = JsonContent.Create(new AssignLopRequest(lopId));
        await _client.SendAsync(assignRequest);

        var removeRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{studentId}/lop", adminToken);
        removeRequest.Content = JsonContent.Create(new AssignLopRequest(null));
        await _client.SendAsync(removeRequest);

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/activity-log", adminToken));
        var log = (await response.Content.ReadFromJsonAsync<ApiResponse<List<LopActivityLogResponse>>>())!.Data!;

        Assert.Contains(log, l => l.ActionType == "StudentAdded" && l.TargetUserId == studentId);
        Assert.Contains(log, l => l.ActionType == "StudentRemoved" && l.TargetUserId == studentId);
        // Mới nhất trước — StudentRemoved xảy ra sau StudentAdded.
        Assert.Equal("StudentRemoved", log[0].ActionType);
    }

    [Fact]
    public async Task Chu_nhiem_can_view_activity_log_of_their_own_lop()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var teacherId = await RegisterTeacherAsync("log-chunhiem", adminToken);

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherId));
        await _client.SendAsync(assignGvRequest);

        var teacherToken = TestTokenService.IssueAccessToken(teacherId.ToString(), "logcn@test.local", "LogCN", Roles.Teacher).AccessToken;
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/activity-log", teacherToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Trọng tâm RBAC 2 chiều theo yêu cầu: Teacher A xem nhật ký lớp Teacher B phụ trách phải bị
    // chặn 403 thật.
    [Fact]
    public async Task Teacher_A_cannot_view_activity_log_of_lop_managed_by_teacher_B()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopBId = await CreateLopAsync("LopB3", khoaId, adminToken);
        var teacherBId = await RegisterTeacherAsync("log-teacher-b", adminToken);

        var assignGvRequest = WithAuth(HttpMethod.Put, $"/api/v1/auth/lop/{lopBId}/giao-vien", adminToken);
        assignGvRequest.Content = JsonContent.Create(new AssignGiaoVienRequest(teacherBId));
        await _client.SendAsync(assignGvRequest);

        var teacherAToken = TeacherToken(out _);
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopBId}/activity-log", teacherAToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_view_activity_log()
    {
        var adminToken = AdminToken(out _);
        var khoaId = await CreateKhoaAsync($"K{Guid.NewGuid():N}"[..8], adminToken);
        var lopId = await CreateLopAsync("CNTT1", khoaId, adminToken);
        var studentId = await RegisterStudentAsync("log-student-forbidden");
        var studentToken = TestTokenService.IssueAccessToken(studentId.ToString(), "ls@test.local", "LS", Roles.Student).AccessToken;

        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/activity-log", studentToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Activity_log_of_unknown_lop_returns_not_found()
    {
        var response = await _client.SendAsync(WithAuth(HttpMethod.Get, $"/api/v1/auth/lop/{Guid.NewGuid()}/activity-log", AdminToken(out _)));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>Bản sao hằng số ChucVu ở tầng test (AuthService.Api.Entities.ChucVuValues là internal
/// theo assembly hiện tại — dùng string literal trực tiếp qua constant này để tránh gõ tay sai chính tả
/// ở nhiều chỗ trong file test).</summary>
internal static class ChucVuValuesForTest
{
    public const string LopTruong = "Lớp trưởng";
}
