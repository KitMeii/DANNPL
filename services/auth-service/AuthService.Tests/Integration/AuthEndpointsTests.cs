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

public sealed class AuthEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_then_login_then_me_returns_correct_role_claim()
    {
        var email = $"student-{Guid.NewGuid():N}@test.local";
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Nguyễn Văn A"));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.NotNull(registerBody);
        Assert.True(registerBody!.Success);
        Assert.Equal(Roles.Student, registerBody.Data!.User.Role);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, "P@ssw0rd123"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.NotNull(loginBody);
        var accessToken = loginBody!.Data!.AccessToken;

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var meResponse = await _client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meBody = await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.NotNull(meBody);
        Assert.Equal(email, meBody!.Data!.Email);
        Assert.Equal(Roles.Student, meBody.Data.Role);
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_conflict()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.local";
        var request = new RegisterRequest(email, "P@ssw0rd123", "Trần Thị B");

        var first = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = await second.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.False(body!.Success);
        Assert.Equal(ErrorCodes.Conflict, body.Error!.Code);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_with_standard_envelope()
    {
        var email = $"wrongpw-{Guid.NewGuid():N}@test.local";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", "Lê Văn C"));

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.False(body!.Success);
        Assert.Equal(ErrorCodes.Unauthorized, body.Error!.Code);
    }

    [Fact]
    public async Task Register_with_weak_password_returns_validation_error()
    {
        var email = $"weak-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "short", "Phạm Thị D"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.False(body!.Success);
        Assert.Equal(ErrorCodes.ValidationError, body.Error!.Code);
    }

    // ===================== Việc 3.1 (2026-08-19) — trường cá nhân tùy chọn =====================

    [Fact]
    public async Task Register_without_optional_personal_fields_still_succeeds()
    {
        var email = $"noopt-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Không Điền Thêm"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.Null(body!.Data!.User.CapBac);
        Assert.Null(body.Data.User.SoDienThoai);
        Assert.Null(body.Data.User.NamHoc);
    }

    [Fact]
    public async Task Register_with_valid_optional_personal_fields_saves_them()
    {
        var email = $"opt-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Có Điền Thêm", CapBacValues.ThuongSi, "0912345678", "2025-2026"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.Equal(CapBacValues.ThuongSi, body!.Data!.User.CapBac);
        Assert.Equal("0912345678", body.Data.User.SoDienThoai);
        Assert.Equal("2025-2026", body.Data.User.NamHoc);
    }

    [Theory]
    [InlineData("123456789")] // 9 số, thiếu 1
    [InlineData("1912345678")] // không bắt đầu bằng 0
    [InlineData("091234567a")] // có ký tự chữ
    public async Task Register_with_invalid_phone_format_returns_validation_error(string badPhone)
    {
        var email = $"badphone-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "SĐT Sai", SoDienThoai: badPhone));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("2025-2027")] // cách nhau 2 năm
    [InlineData("2026-2025")] // ngược
    [InlineData("2025/2026")] // sai ký tự phân cách
    public async Task Register_with_invalid_nam_hoc_format_returns_validation_error(string badNamHoc)
    {
        var email = $"badnamhoc-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Năm Học Sai", NamHoc: badNamHoc));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_capbac_outside_fixed_list_returns_validation_error()
    {
        var email = $"badcapbac-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Cấp Bậc Sai", CapBac: "Đại tướng"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Student_can_update_personal_fields_via_profile()
    {
        var (client, token) = await RegisterAndLoginAsync("profileupd");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new UpdateProfileRequest(
            "Tên Đã Sửa", CapBacValues.TrungUy, "0987654321", "2026-2027"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal("Tên Đã Sửa", body!.Data!.Name);
        Assert.Equal(CapBacValues.TrungUy, body.Data.CapBac);
        Assert.Equal("0987654321", body.Data.SoDienThoai);
        Assert.Equal("2026-2027", body.Data.NamHoc);
    }

    [Fact]
    public async Task Student_sending_bomonkhoa_via_profile_is_silently_ignored_not_an_error()
    {
        // BoMonKhoa chỉ áp dụng Teacher — Student gửi lên không lỗi, chỉ đơn giản không được ghi
        // (xem AuthServiceImpl.UpdateProfileAsync — mỗi field chỉ áp dụng đúng role).
        var (client, token) = await RegisterAndLoginAsync("studentbmk");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new UpdateProfileRequest("Tên", BoMonKhoa: "Khoa CNTT"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Null(body!.Data!.BoMonKhoa);
    }

    private async Task<(HttpClient client, string token)> RegisterAndLoginAsync(string prefix)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@test.local";
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Người Dùng Test"));
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return (_client, registerBody!.Data!.AccessToken);
    }

    // Rà soát Lần XVI (2026-08-21) — Admin trước đây hoàn toàn không tự sửa được SĐT/Cấp bậc/Chức
    // vụ qua Hồ sơ cá nhân (UpdateProfileAsync's else-if không có nhánh Admin) — người dùng yêu cầu
    // Admin cũng cần đủ như GV. Đăng ký 1 Student thật rồi promote lên Admin (JWT tổng hợp CHỈ để
    // gọi PUT .../role — không cần tồn tại trong DB, middleware chỉ kiểm claim Role=Admin) để lấy 1
    // phiên đăng nhập Admin THẬT (đăng nhập lại sau khi promote), test đúng UpdateProfileAsync self-
    // service qua PUT /me.
    private static readonly JwtTokenService TestTokenService = new(Options.Create(new JwtOptions
    {
        Issuer = "tthcm-platform",
        Audience = "tthcm-services",
        SigningKey = "dev-only-signing-key-do-not-use-in-production-min-32-chars",
    }));

    private async Task<(HttpClient client, string token)> RegisterAndPromoteToAdminAsync(string prefix)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@test.local";
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Admin Test"));
        var userId = (await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!.User.Id;

        var bootstrapAdminId = Guid.NewGuid();
        var bootstrapAdminToken = TestTokenService.IssueAccessToken(
            bootstrapAdminId.ToString(), "bootstrap-admin@test.local", "Bootstrap Admin", Roles.Admin).AccessToken;
        var promoteRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/auth/users/{userId}/role");
        promoteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bootstrapAdminToken);
        promoteRequest.Headers.Add("X-Internal-Key", AuthApiFactory.TestInternalServiceKey);
        promoteRequest.Content = JsonContent.Create(new ChangeRoleRequest(Roles.Admin));
        await _client.SendAsync(promoteRequest);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var adminToken = (await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!.AccessToken;
        return (_client, adminToken);
    }

    [Fact]
    public async Task Admin_can_update_sdt_cap_bac_and_chuc_vu_via_profile()
    {
        var (client, token) = await RegisterAndPromoteToAdminAsync("adminprofile");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new UpdateProfileRequest(
            "Admin Đã Sửa", CapBacValues.DaiTa, "0912345678", ChucVuGV: ChucVuGvValues.TruongKhoa));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Equal("Admin Đã Sửa", body!.Data!.Name);
        Assert.Equal(CapBacValues.DaiTa, body.Data.CapBac);
        Assert.Equal("0912345678", body.Data.SoDienThoai);
        Assert.Equal(ChucVuGvValues.TruongKhoa, body.Data.ChucVuGV);
    }

    [Fact]
    public async Task Admin_sending_bomonkhoa_via_profile_is_silently_ignored_not_an_error()
    {
        // BoMonKhoa CHỈ áp dụng Teacher (xem AuthServiceImpl.UpdateProfileAsync) — Admin gửi lên
        // không lỗi, chỉ đơn giản không được ghi, cùng hành vi với Student ở test phía trên.
        var (client, token) = await RegisterAndPromoteToAdminAsync("adminbmk");

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new UpdateProfileRequest("Tên", BoMonKhoa: "Khoa CNTT"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
        Assert.Null(body!.Data!.BoMonKhoa);
    }
}
