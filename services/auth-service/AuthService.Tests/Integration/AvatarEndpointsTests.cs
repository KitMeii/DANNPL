using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace AuthService.Tests.Integration;

public sealed class AvatarEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public AvatarEndpointsTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAsync(string namePrefix)
    {
        var email = $"{namePrefix}-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "P@ssw0rd123", namePrefix));
        var auth = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;
        return auth.AccessToken;
    }

    private static HttpRequestMessage BuildUploadRequest(string token, byte[] bytes, string fileName, string contentType)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/me/avatar");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = new MultipartFormDataContent { { fileContent, "file", fileName } };
        return request;
    }

    [Fact]
    public async Task Unauthenticated_upload_is_rejected()
    {
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var content = new MultipartFormDataContent { { fileContent, "file", "avatar.png" } };

        var response = await _client.PostAsync("/api/v1/auth/me/avatar", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Any_authenticated_user_can_upload_their_own_avatar_and_it_persists()
    {
        var uploadCountBefore = _factory.AvatarStorage.UploadCallCount;
        var token = await RegisterAsync("avatar-basic");

        var response = await _client.SendAsync(BuildUploadRequest(token, [1, 2, 3, 4, 5], "avatar.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.NotNull(body.AvatarUrl);
        Assert.Contains("res.cloudinary.com", body.AvatarUrl);
        Assert.Equal(uploadCountBefore + 1, _factory.AvatarStorage.UploadCallCount);

        // Xác nhận lưu thật, không phải chỉ trả về trong response — gọi /me riêng để verify.
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var meResponse = await _client.SendAsync(meRequest);
        var me = (await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;
        Assert.Equal(body.AvatarUrl, me.AvatarUrl);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public async Task Accepts_all_3_allowed_content_types(string contentType)
    {
        var token = await RegisterAsync("avatar-type");
        var response = await _client.SendAsync(BuildUploadRequest(token, [1, 2, 3], $"a.{contentType.Split('/')[1]}", contentType));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_non_image_content_type()
    {
        var token = await RegisterAsync("avatar-badtype");
        var response = await _client.SendAsync(BuildUploadRequest(token, [1, 2, 3], "a.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_empty_file()
    {
        var token = await RegisterAsync("avatar-empty");
        var response = await _client.SendAsync(BuildUploadRequest(token, [], "empty.png", "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_file_over_5mb()
    {
        var token = await RegisterAsync("avatar-toobig");
        var oversized = new byte[5 * 1024 * 1024 + 1];
        var response = await _client.SendAsync(BuildUploadRequest(token, oversized, "big.png", "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Uploading_a_second_avatar_deletes_the_old_one_from_storage()
    {
        var token = await RegisterAsync("avatar-replace");

        var first = await _client.SendAsync(BuildUploadRequest(token, [1, 2, 3], "first.png", "image/png"));
        var firstBody = (await first.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;

        var deleteCountBefore = _factory.AvatarStorage.DeleteCallCount;
        var second = await _client.SendAsync(BuildUploadRequest(token, [4, 5, 6], "second.png", "image/png"));
        var secondBody = (await second.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(firstBody.AvatarUrl, secondBody.AvatarUrl);
        Assert.Equal(deleteCountBefore + 1, _factory.AvatarStorage.DeleteCallCount);
    }

    // Trọng tâm RBAC: endpoint không nhận id từ client (chỉ suy ra từ JWT), nên 2 user khác nhau
    // đổi avatar không thể ảnh hưởng lẫn nhau — verify user B's avatar vẫn null sau khi user A đổi.
    [Fact]
    public async Task Uploading_avatar_does_not_affect_other_users()
    {
        var tokenA = await RegisterAsync("avatar-userA");
        var tokenB = await RegisterAsync("avatar-userB");

        await _client.SendAsync(BuildUploadRequest(tokenA, [1, 2, 3], "a.png", "image/png"));

        using var meBRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meBRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var meBResponse = await _client.SendAsync(meBRequest);
        var meB = (await meBResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>())!.Data!;

        Assert.Null(meB.AvatarUrl);
    }
}
