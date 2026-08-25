using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QuizService.Api.Clients;

namespace QuizService.Tests.Integration;

/// <summary>Dictionary chia sẻ, sống độc lập với vòng đời request — QuizApiFactory expose 1 thực
/// thể public để test seed trước khi gọi API (giống ProgressReporter/FakeProgressReporter).</summary>
public sealed class AuthQuizClientSeed
{
    public Dictionary<Guid, Guid?> LopIdByUser { get; } = new();
    public Dictionary<Guid, List<Guid>> OwnedLopIdsByUser { get; } = new();

    /// <summary>Việc C — roster theo LopId (không phụ thuộc "người gọi hiện tại" như 2 dictionary
    /// trên, vì ListHocVienAsync nhận thẳng lopId làm tham số).</summary>
    public Dictionary<Guid, List<RemoteHocVien>> RosterByLop { get; } = new();
}

/// <summary>Đứng thay cho auth-service thật — đọc userId từ chính JWT của request đang xử lý
/// (giống hệt cách HttpAuthQuizClient forward JWT gốc sang auth-service, chỉ khác là tra cứu local
/// thay vì gọi HTTP), rồi tra <see cref="AuthQuizClientSeed"/>. IHttpContextAccessor ở đây PHẢI là
/// instance do DI container tự quản lý (đăng ký qua builder.Services.AddHttpContextAccessor() ở
/// Program.cs) — đó là instance duy nhất được framework tự set .HttpContext mỗi request.</summary>
public sealed class FakeAuthQuizClient(IHttpContextAccessor httpContextAccessor, AuthQuizClientSeed seed) : IAuthQuizClient
{
    public Task<Guid?> GetMyLopIdAsync(CancellationToken ct)
    {
        var userId = CurrentUserId();
        return Task.FromResult(userId is not null && seed.LopIdByUser.TryGetValue(userId.Value, out var lopId) ? lopId : null);
    }

    public Task<IReadOnlyList<Guid>> ListMyLopIdsAsync(CancellationToken ct)
    {
        var userId = CurrentUserId();
        IReadOnlyList<Guid> result = userId is not null && seed.OwnedLopIdsByUser.TryGetValue(userId.Value, out var ids) ? ids : [];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<RemoteHocVien>> ListHocVienAsync(Guid lopId, CancellationToken ct)
    {
        IReadOnlyList<RemoteHocVien> result = seed.RosterByLop.TryGetValue(lopId, out var roster) ? roster : [];
        return Task.FromResult(result);
    }

    private Guid? CurrentUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var idClaim = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        return idClaim is not null ? Guid.Parse(idClaim) : null;
    }
}
