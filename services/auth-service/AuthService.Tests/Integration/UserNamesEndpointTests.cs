using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace AuthService.Tests.Integration;

public sealed class UserNamesEndpointTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public UserNamesEndpointTests(AuthApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Returns_names_for_the_requested_ids_only()
    {
        var email1 = $"n1-{Guid.NewGuid():N}@test.local";
        var email2 = $"n2-{Guid.NewGuid():N}@test.local";

        var register1 = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email1, "P@ssw0rd123", "Người Một"));
        var register2 = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email2, "P@ssw0rd123", "Người Hai"));
        var user1 = (await register1.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;
        var user2 = (await register2.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>())!.Data!;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/names?ids={user1.User.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserNameResponse>>>();
        Assert.NotNull(body!.Data);
        var entry = Assert.Single(body.Data);
        Assert.Equal("Người Một", entry.Name);
        Assert.DoesNotContain(body.Data, n => n.Id == user2.User.Id);
    }

    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        var response = await _client.GetAsync($"/api/v1/auth/users/names?ids={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
