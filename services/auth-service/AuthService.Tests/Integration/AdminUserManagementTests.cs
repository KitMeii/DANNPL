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

public sealed class AdminUserManagementTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public AdminUserManagementTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage WithAuth(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    // GET /users and PUT /users/{id}/role additionally require X-Internal-Key — simulates the
    // one legitimate caller (admin-service's HttpAuthAdminClient). See RequireInternalServiceKeyFilter.
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

    [Fact]
    public async Task Student_cannot_list_users()
    {
        var email = $"student-list-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", "Student"));
        var studentToken = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!.AccessToken;

        var request = WithAuth(HttpMethod.Get, "/api/v1/auth/users", studentToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_change_a_students_role_and_it_persists()
    {
        var email = $"promote-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", "Học viên X"));
        var student = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;
        var adminToken = AdminToken(out _);

        var changeRoleRequest = WithAuthAndInternalKey(HttpMethod.Put, $"/api/v1/auth/users/{student.User.Id}/role", adminToken);
        changeRoleRequest.Content = JsonContent.Create(new ChangeRoleRequest(Roles.Teacher));
        var response = await _client.SendAsync(changeRoleRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal(Roles.Teacher, body!.Data!.Role);

        // Confirm it actually persisted, not just echoed back in the response.
        var listRequest = WithAuthAndInternalKey(HttpMethod.Get, "/api/v1/auth/users?role=Teacher", adminToken);
        var listResponse = await _client.SendAsync(listRequest);
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserResponse>>>())!.Data!;
        Assert.Contains(list, u => u.Id == student.User.Id);
    }

    // Việc 3.1 (2026-08-19) — BoMonKhoa chỉ có ý nghĩa cho Teacher; đối xứng với
    // AuthEndpointsTests.Student_sending_bomonkhoa_via_profile_is_silently_ignored_not_an_error
    // (Student gửi lên bị bỏ qua) — ở đây xác nhận chiều ngược lại: Teacher gửi lên PHẢI được lưu.
    [Fact]
    public async Task Teacher_can_save_bomonkhoa_via_profile()
    {
        var email = $"teacherbmk-{Guid.NewGuid():N}@test.local";
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Giáo Viên Test"));
        var studentUser = (await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!.User;

        var promoteRequest = WithAuthAndInternalKey(HttpMethod.Put, $"/api/v1/auth/users/{studentUser.Id}/role", AdminToken(out _));
        promoteRequest.Content = JsonContent.Create(new ChangeRoleRequest(Roles.Teacher));
        await _client.SendAsync(promoteRequest);

        // Đăng nhập lại để lấy JWT mới phản ánh đúng Role=Teacher (token cũ vẫn mang claim Student).
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var teacherToken = (await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!.AccessToken;

        var updateRequest = WithAuth(HttpMethod.Put, "/api/v1/auth/me", teacherToken);
        updateRequest.Content = JsonContent.Create(new UpdateProfileRequest("Giáo Viên Test", BoMonKhoa: "Khoa Công nghệ thông tin"));
        var updateResponse = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var body = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal("Khoa Công nghệ thông tin", body!.Data!.BoMonKhoa);
    }

    [Fact]
    public async Task Admin_cannot_change_own_role()
    {
        var adminToken = AdminToken(out var adminId);

        var request = WithAuthAndInternalKey(HttpMethod.Put, $"/api/v1/auth/users/{adminId}/role", adminToken);
        request.Content = JsonContent.Create(new ChangeRoleRequest(Roles.Student));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Change_role_rejects_unknown_role_value()
    {
        var email = $"badrole-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", "Học viên Y"));
        var student = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;

        var request = WithAuthAndInternalKey(HttpMethod.Put, $"/api/v1/auth/users/{student.User.Id}/role", AdminToken(out _));
        request.Content = JsonContent.Create(new ChangeRoleRequest("SuperAdmin"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Phần A RBAC audit: these two admin-only endpoints must not be callable with just a valid
    // Admin JWT — that would let any client bypass admin-service's audit log by hitting
    // auth-service directly through the gateway's /api/v1/auth/** catch-all route. Only a caller
    // that also knows InternalService:SharedKey (i.e. admin-service) may proceed.
    [Fact]
    public async Task Admin_without_internal_key_cannot_change_role()
    {
        var email = $"bypass-attempt-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", "Học viên Z"));
        var student = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;

        var request = WithAuth(HttpMethod.Put, $"/api/v1/auth/users/{student.User.Id}/role", AdminToken(out _));
        request.Content = JsonContent.Create(new ChangeRoleRequest(Roles.Teacher));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_without_internal_key_cannot_list_users()
    {
        var request = WithAuth(HttpMethod.Get, "/api/v1/auth/users", AdminToken(out _));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ═══ Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp + khóa/mở khóa tài khoản ═══

    [Fact]
    public async Task Admin_can_create_a_teacher_account_directly_and_it_can_log_in()
    {
        var email = $"admin-created-teacher-{Guid.NewGuid():N}@test.local";
        var createRequest = WithAuthAndInternalKey(HttpMethod.Post, "/api/v1/auth/users", AdminToken(out _));
        createRequest.Content = JsonContent.Create(new CreateUserByAdminRequest(email, "P@ssw0rd123", "GV Do Admin Tạo", Roles.Teacher));
        var createResponse = await _client.SendAsync(createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(Roles.Teacher, created.Role);
        Assert.False(created.IsLocked);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loggedIn = (await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;
        Assert.Equal(Roles.Teacher, loggedIn.User.Role);
    }

    [Fact]
    public async Task Creating_a_user_with_a_taken_email_returns_conflict()
    {
        var email = $"admin-created-dup-{Guid.NewGuid():N}@test.local";
        var firstRequest = WithAuthAndInternalKey(HttpMethod.Post, "/api/v1/auth/users", AdminToken(out _));
        firstRequest.Content = JsonContent.Create(new CreateUserByAdminRequest(email, "P@ssw0rd123", "GV 1", Roles.Teacher));
        await _client.SendAsync(firstRequest);

        var secondRequest = WithAuthAndInternalKey(HttpMethod.Post, "/api/v1/auth/users", AdminToken(out _));
        secondRequest.Content = JsonContent.Create(new CreateUserByAdminRequest(email, "P@ssw0rd123", "GV 2", Roles.Teacher));
        var secondResponse = await _client.SendAsync(secondRequest);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_without_internal_key_cannot_create_a_user()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/auth/users", AdminToken(out _));
        request.Content = JsonContent.Create(new CreateUserByAdminRequest($"bypass-create-{Guid.NewGuid():N}@test.local", "P@ssw0rd123", "X", Roles.Student));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Locked_account_cannot_log_in_and_unlocking_restores_access()
    {
        var email = $"lock-test-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", "Học viên Khóa"));
        var student = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;

        var lockRequest = WithAuthAndInternalKey(HttpMethod.Put, $"/api/v1/auth/users/{student.User.Id}/locked", AdminToken(out _));
        lockRequest.Content = JsonContent.Create(new SetUserLockedRequest(true));
        var lockResponse = await _client.SendAsync(lockRequest);
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);
        Assert.True((await lockResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!.IsLocked);

        var blockedLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        Assert.Equal(HttpStatusCode.Unauthorized, blockedLogin.StatusCode);

        var unlockRequest = WithAuthAndInternalKey(HttpMethod.Put, $"/api/v1/auth/users/{student.User.Id}/locked", AdminToken(out _));
        unlockRequest.Content = JsonContent.Create(new SetUserLockedRequest(false));
        await _client.SendAsync(unlockRequest);

        var restoredLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        Assert.Equal(HttpStatusCode.OK, restoredLogin.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_lock_own_account()
    {
        var adminToken = AdminToken(out var adminId);

        var request = WithAuthAndInternalKey(HttpMethod.Put, $"/api/v1/auth/users/{adminId}/locked", adminToken);
        request.Content = JsonContent.Create(new SetUserLockedRequest(true));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
