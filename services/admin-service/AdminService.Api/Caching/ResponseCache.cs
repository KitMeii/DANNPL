using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AdminService.Api.Caching;

/// <summary>
/// Cache cho các phép tổng hợp cross-service tốn kém (Việc 7 — Dashboard "Theo dõi Giáo viên" gọi
/// nhiều lượt sang auth-service/quiz-service/content-service để tổng hợp). Cùng pattern y hệt
/// AiService.Api.Caching.ResponseCache (không tách ra Shared.Infrastructure để tránh refactor rộng
/// ngoài phạm vi Việc 7 — 2 bản trùng nhỏ, mỗi bản ~70 dòng, chấp nhận được). Backed bởi
/// IDistributedCache: Redis khi có Redis:ConnectionString (docker-compose), in-process fallback
/// khi không (local dev không có Redis, test suite). Best-effort: lỗi cache không chặn request
/// thật, chỉ log rồi tính lại từ đầu.
/// </summary>
public sealed class ResponseCache(IDistributedCache cache, ILogger<ResponseCache> logger)
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public async Task<T> GetOrCreateAsync<T>(string scope, string input, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        var key = BuildKey(scope, input);

        var cached = await TryGetAsync<T>(key);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory();
        await TrySetAsync(key, value, ttl ?? DefaultTtl);
        return value;
    }

    private async Task<T?> TryGetAsync<T>(string key)
    {
        try
        {
            var bytes = await cache.GetAsync(key);
            return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for key {Key}; falling back to a live call.", key);
            return default;
        }
    }

    private async Task TrySetAsync<T>(string key, T value, TimeSpan ttl)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for key {Key}; response will not be cached.", key);
        }
    }

    private static string BuildKey(string scope, string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"{scope}:{Convert.ToHexString(hash)}";
    }
}
