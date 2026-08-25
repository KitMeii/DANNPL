using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Grading;

/// <summary>
/// Forwards the calling student's own JWT to ai-service's /grade-oral endpoint. ai-service
/// requires authentication like every other route (uniform policy, no separate internal-service
/// auth mechanism to maintain) — this works because oral/submit here already runs behind
/// [Authorize], so the bearer token on the incoming request is exactly the credential ai-service
/// needs to see to accept the call.
/// </summary>
public sealed class HttpOralGradingClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor) : IOralGradingClient
{
    public async Task<OralGradingResult> GradeAsync(OralGradingRequest request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/grade-oral")
        {
            Content = JsonContent.Create(request),
        };

        var incomingAuth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(incomingAuth) && AuthenticationHeaderValue.TryParse(incomingAuth, out var parsed))
        {
            message.Headers.Authorization = parsed;
        }

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OralGradingResult>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("ai-service returned an empty grading response.");

        return body.Data ?? throw new InvalidOperationException("ai-service returned a successful response with no grading data.");
    }
}
