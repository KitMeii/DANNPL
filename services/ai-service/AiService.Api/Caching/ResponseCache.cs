using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AiService.Api.Caching;

/// <summary>
/// Cache for repeated identical AI requests (e.g. two students opening the same lecture chapter
/// both trigger a "generate lecture narration" call with the same source text) — avoids paying
/// for the same Groq completion twice. Backed by IDistributedCache: Redis when
/// Redis:ConnectionString is configured (docker-compose/production, shared across every
/// ai-service replica), an in-process fallback otherwise (local `dotnet run` without Redis, and
/// the test suite — see Program.cs). Cache reads/writes are best-effort: if Redis is unreachable,
/// we log and fall through to calling Groq directly rather than failing the request — caching is
/// a cost/latency optimization, not a correctness dependency.
/// </summary>
public sealed class ResponseCache(IDistributedCache cache, ILogger<ResponseCache> logger)
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(1);

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
