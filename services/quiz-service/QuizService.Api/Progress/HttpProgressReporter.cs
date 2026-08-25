using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Auth;

namespace QuizService.Api.Progress;

/// <summary>Reports a graded score to progress-service. That endpoint also requires the
/// X-Internal-Key header (see RequireInternalServiceKeyFilter) — this is the only place that
/// header should ever be sent from, since quiz-service is its one legitimate caller (progress
/// -service must never trust a client-supplied score forwarded on a student's own JWT alone).</summary>
public sealed class HttpProgressReporter(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    IOptions<InternalServiceAuthOptions> internalServiceAuthOptions) : IProgressReporter
{
    private sealed record RecordScoreRequest(decimal Score);

    public async Task ReportScoreAsync(decimal score, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/progress/record-score")
        {
            Content = JsonContent.Create(new RecordScoreRequest(score)),
        };

        var incomingAuth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(incomingAuth) && AuthenticationHeaderValue.TryParse(incomingAuth, out var parsed))
        {
            message.Headers.Authorization = parsed;
        }

        message.Headers.Add(RequireInternalServiceKeyFilter.HeaderName, internalServiceAuthOptions.Value.SharedKey);

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();
    }
}
