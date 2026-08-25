using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;

namespace AdminService.Api.Clients;

/// <summary>Calls quiz-service's admin-only batch score endpoint. Sends X-Internal-Key — see
/// HttpAuthAdminClient remarks for the same convention (admin-service is the one legitimate
/// caller).</summary>
public sealed class HttpQuizStatsClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    IOptions<InternalServiceAuthOptions> internalServiceAuthOptions) : IQuizStatsClient
{
    private sealed record ScoresByUsersRequestBody(IReadOnlyList<Guid> UserIds);

    private sealed record ScoresByUsersResponseBody(IReadOnlyList<RemoteUserScore> Users);

    public async Task<IReadOnlyList<RemoteUserScore>> GetScoresByUsersAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/quiz/stats/scores-by-users")
        {
            Content = JsonContent.Create(new ScoresByUsersRequestBody(userIds)),
        };

        var incomingAuth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(incomingAuth) && AuthenticationHeaderValue.TryParse(incomingAuth, out var parsed))
        {
            message.Headers.Authorization = parsed;
        }

        message.Headers.Add(RequireInternalServiceKeyFilter.HeaderName, internalServiceAuthOptions.Value.SharedKey);

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ScoresByUsersResponseBody>>(cancellationToken: ct);
        return body?.Data?.Users ?? [];
    }
}
