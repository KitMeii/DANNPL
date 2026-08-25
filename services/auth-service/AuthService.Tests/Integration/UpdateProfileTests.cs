using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace AuthService.Tests.Integration;

public sealed class UpdateProfileTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public UpdateProfileTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Update_profile_persists_name()
    {
        var email = $"profile-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", "Tên Cũ"));
        var auth = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/me")
        {
            Content = JsonContent.Create(new UpdateProfileRequest("Tên Mới")),
        };
        updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var updateResponse = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal("Tên Mới", updated.Name);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var meResponse = await _client.SendAsync(meRequest);
        var me = (await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;

        Assert.Equal("Tên Mới", me.Name);
    }

    // ---------------------------------------------------------------------------
    // Rà soát Lần VI (2026-08-21) — đổi mật khẩu self-service, mọi role.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Change_password_with_correct_current_password_allows_login_with_new_password()
    {
        var email = $"pwd-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "OldP@ss123", "Đổi Mật Khẩu"));
        var auth = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;

        using var changeRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/me/password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest("OldP@ss123", "NewP@ss456")),
        };
        changeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var changeResponse = await _client.SendAsync(changeRequest);
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        var loginOld = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "OldP@ss123"));
        Assert.Equal(HttpStatusCode.Unauthorized, loginOld.StatusCode);

        var loginNew = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "NewP@ss456"));
        Assert.Equal(HttpStatusCode.OK, loginNew.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_is_rejected()
    {
        var email = $"pwd-wrong-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "OldP@ss123", "Sai Mật Khẩu Cũ"));
        var auth = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;

        using var changeRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/me/password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest("SaiMatKhau999", "NewP@ss456")),
        };
        changeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var changeResponse = await _client.SendAsync(changeRequest);

        // 403 (không phải 401) — cố ý, xem comment ở AuthServiceImpl.ChangePasswordAsync: 401 kích
        // hoạt xử lý "phiên hết hạn" toàn cục ở frontend, sẽ đăng xuất nhầm người dùng hợp lệ.
        Assert.Equal(HttpStatusCode.Forbidden, changeResponse.StatusCode);

        var loginStillOld = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "OldP@ss123"));
        Assert.Equal(HttpStatusCode.OK, loginStillOld.StatusCode);
    }
}
