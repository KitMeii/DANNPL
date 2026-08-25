using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Shared.Infrastructure.Common;

namespace ProgressService.Api.Clients;

public sealed class HttpUserNameLookupClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor) : IUserNameLookupClient
{
    private sealed record UserNameResponse(Guid Id, string Name, string Role);

    public async Task<IReadOnlyDictionary<Guid, UserName>> GetNamesAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserName>();
        }

        var idsParam = string.Join(',', userIds);
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/names?ids={idsParam}");

        var incomingAuth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(incomingAuth) && AuthenticationHeaderValue.TryParse(incomingAuth, out var parsed))
        {
            message.Headers.Authorization = parsed;
        }

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserNameResponse>>>(cancellationToken: ct);
        return body?.Data?.ToDictionary(u => u.Id, u => new UserName(u.Id, u.Name, u.Role)) ?? new Dictionary<Guid, UserName>();
    }
}
