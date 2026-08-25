using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Clients;

public sealed class HttpAuthQuizClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    IOptions<InternalServiceAuthOptions> internalServiceAuthOptions) : IAuthQuizClient
{
    private sealed record MeResponse(Guid Id, string Email, string Name, string Role, Guid? LopId, string ChucVu, string? AvatarUrl);
    private sealed record LopIdOnly(Guid Id);
    private sealed record HocVienIdResponse(Guid Id, string Name, string ChucVu, string? CapBac, string? AvatarUrl);

    public async Task<Guid?> GetMyLopIdAsync(CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Get, "/api/v1/auth/me");
        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MeResponse>>(cancellationToken: ct);
        return body?.Data?.LopId;
    }

    public async Task<IReadOnlyList<Guid>> ListMyLopIdsAsync(CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Get, "/api/v1/auth/lop/mine");
        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<LopIdOnly>>>(cancellationToken: ct);
        return body?.Data?.Select(l => l.Id).ToList() ?? [];
    }

    public async Task<IReadOnlyList<RemoteHocVien>> ListHocVienAsync(Guid lopId, CancellationToken ct)
    {
        // Việc C — endpoint service-to-service, cần thêm X-Internal-Key ngoài JWT forward (khác 2
        // hàm trên) vì auth-service tin caller đã tự kiểm ownership, xem
        // KhoaLopEndpoints.ListHocVienIdsAsync remarks.
        using var message = ForwardedRequest(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/hoc-vien-ids");
        message.Headers.Add(RequireInternalServiceKeyFilter.HeaderName, internalServiceAuthOptions.Value.SharedKey);

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<HocVienIdResponse>>>(cancellationToken: ct);
        return body?.Data?.Select(u => new RemoteHocVien(u.Id, u.Name, u.ChucVu, u.CapBac, u.AvatarUrl)).ToList() ?? [];
    }

    private HttpRequestMessage ForwardedRequest(HttpMethod method, string url)
    {
        var message = new HttpRequestMessage(method, url);
        var incomingAuth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(incomingAuth) && AuthenticationHeaderValue.TryParse(incomingAuth, out var parsed))
        {
            message.Headers.Authorization = parsed;
        }

        return message;
    }
}
