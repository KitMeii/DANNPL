using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;

namespace AdminService.Api.Clients;

public sealed class HttpQuizLopDataClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    IOptions<InternalServiceAuthOptions> internalServiceAuthOptions) : IQuizLopDataClient
{
    public async Task<RemoteQuizLopDataDump> DumpAsync(IReadOnlyList<Guid> userIds, Guid lopId, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Post, "/internal/lop-data/dump");
        message.Content = JsonContent.Create(new { UserIds = userIds, LopId = lopId });

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RemoteQuizLopDataDump>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("quiz-service returned an empty lop-data dump response.");

        return body.Data ?? throw new InvalidOperationException("quiz-service returned a successful response with no dump data.");
    }

    public async Task<RemoteQuizLopDataDeleteResult> DeleteAsync(IReadOnlyList<Guid> userIds, Guid lopId, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Post, "/internal/lop-data/delete");
        message.Content = JsonContent.Create(new { UserIds = userIds, LopId = lopId });

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RemoteQuizLopDataDeleteResult>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("quiz-service returned an empty lop-data delete response.");

        return body.Data ?? throw new InvalidOperationException("quiz-service returned a successful response with no delete result.");
    }

    private HttpRequestMessage ForwardedRequest(HttpMethod method, string url)
    {
        var message = new HttpRequestMessage(method, url);
        var incomingAuth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(incomingAuth) && AuthenticationHeaderValue.TryParse(incomingAuth, out var parsed))
        {
            message.Headers.Authorization = parsed;
        }

        message.Headers.Add(RequireInternalServiceKeyFilter.HeaderName, internalServiceAuthOptions.Value.SharedKey);

        return message;
    }
}
