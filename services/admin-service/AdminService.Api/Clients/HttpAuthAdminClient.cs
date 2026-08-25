using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;

namespace AdminService.Api.Clients;

/// <summary>Calls auth-service's admin-only user endpoints. Those endpoints also require the
/// X-Internal-Key header (see RequireInternalServiceKeyFilter) — this is the only place that
/// header should ever be sent from, since admin-service is their one legitimate caller.</summary>
public sealed class HttpAuthAdminClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    IOptions<InternalServiceAuthOptions> internalServiceAuthOptions) : IAuthAdminClient
{
    public async Task<IReadOnlyList<RemoteUser>> ListUsersAsync(string? role, Guid? lopId, Guid? khoaId, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(role)) query.Add($"role={role}");
        if (lopId is not null) query.Add($"lopId={lopId}");
        if (khoaId is not null) query.Add($"khoaId={khoaId}");
        var url = query.Count == 0 ? "/api/v1/auth/users" : $"/api/v1/auth/users?{string.Join('&', query)}";
        using var message = ForwardedRequest(HttpMethod.Get, url);

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<RemoteUser>>>(cancellationToken: ct);
        return body?.Data ?? [];
    }

    public async Task<IReadOnlyList<RemoteUser>> ListHocVienAsync(Guid lopId, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/hoc-vien");

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<RemoteUser>>>(cancellationToken: ct);
        return body?.Data ?? [];
    }

    public async Task<RemoteUser> ChangeRoleAsync(Guid userId, string newRole, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Put, $"/api/v1/auth/users/{userId}/role");
        message.Content = JsonContent.Create(new { Role = newRole });

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RemoteUser>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("auth-service returned an empty role-change response.");

        return body.Data ?? throw new InvalidOperationException("auth-service returned a successful response with no user data.");
    }

    public async Task<RemoteUser> CreateUserAsync(string email, string password, string name, string role, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Post, "/api/v1/auth/users");
        message.Content = JsonContent.Create(new { Email = email, Password = password, Name = name, Role = role });

        var response = await httpClient.SendAsync(message, ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ConflictException("Email đã được đăng ký.");
        }
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RemoteUser>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("auth-service returned an empty create-user response.");

        return body.Data ?? throw new InvalidOperationException("auth-service returned a successful response with no user data.");
    }

    public async Task<RemoteUser> SetUserLockedAsync(Guid userId, bool isLocked, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Put, $"/api/v1/auth/users/{userId}/locked");
        message.Content = JsonContent.Create(new { IsLocked = isLocked });

        var response = await httpClient.SendAsync(message, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Không tìm thấy người dùng.");
        }
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RemoteUser>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("auth-service returned an empty lock response.");

        return body.Data ?? throw new InvalidOperationException("auth-service returned a successful response with no user data.");
    }

    public async Task<RemoteKhoa> GetKhoaAsync(Guid khoaId, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Get, $"/api/v1/auth/khoa/{khoaId}");

        var response = await httpClient.SendAsync(message, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Không tìm thấy khóa.");
        }
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RemoteKhoa>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("auth-service returned an empty khoa response.");

        return body.Data ?? throw new InvalidOperationException("auth-service returned a successful response with no khoa data.");
    }

    public async Task<IReadOnlyList<RemoteLop>> ListLopAsync(CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Get, "/api/v1/auth/lop");

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<RemoteLop>>>(cancellationToken: ct);
        return body?.Data ?? [];
    }

    public async Task<RemoteLop> GetLopAsync(Guid lopId, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}");

        var response = await httpClient.SendAsync(message, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Không tìm thấy lớp.");
        }
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RemoteLop>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("auth-service returned an empty lop response.");

        return body.Data ?? throw new InvalidOperationException("auth-service returned a successful response with no lop data.");
    }

    public async Task<IReadOnlyList<RemoteLopActivityLog>> ListAllLopActivityLogAsync(Guid lopId, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Get, $"/api/v1/auth/lop/{lopId}/activity-log/all");

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<RemoteLopActivityLog>>>(cancellationToken: ct);
        return body?.Data ?? [];
    }

    public async Task<RemoteDeleteAllLopDataResult> DeleteAllLopDataAsync(Guid lopId, CancellationToken ct)
    {
        using var message = ForwardedRequest(HttpMethod.Post, $"/api/v1/auth/lop/{lopId}/delete-all-data");

        var response = await httpClient.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RemoteDeleteAllLopDataResult>>(cancellationToken: ct)
            ?? throw new InvalidOperationException("auth-service returned an empty delete-all-data response.");

        return body.Data ?? throw new InvalidOperationException("auth-service returned a successful response with no delete-all-data result.");
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
