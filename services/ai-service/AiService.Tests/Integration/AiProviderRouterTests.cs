using AiService.Api.AiProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiService.Tests.Integration;

/// <summary>Unit tests for AiProviderRouter's retry/failover algorithm in isolation (no HTTP, no
/// ASP.NET pipeline) — see AiEndpointsTests.cs for the same behavior exercised end-to-end through
/// real endpoints with a single provider.</summary>
public sealed class AiProviderRouterTests
{
    private static readonly IReadOnlyList<AiMessage> SomeMessages = [new("user", "hỏi gì đó")];

    private static AiProviderRouter BuildRouter(params IAiProvider[] providers) =>
        new(new ListAiProviderFactory(providers), NullLogger<AiProviderRouter>.Instance);

    [Fact]
    public async Task Falls_over_to_the_next_provider_when_the_first_is_permanently_broken()
    {
        var a = new FakeAiProvider("a") { AlwaysThrow = new AiProviderPermanentException("a", "gãy vĩnh viễn") };
        var b = new FakeAiProvider("b") { NextResponse = "trả lời từ b" };
        var router = BuildRouter(a, b);

        var result = await router.CompleteTextAsync(SomeMessages, 100, CancellationToken.None);

        Assert.Equal("trả lời từ b", result);
        Assert.Equal(1, a.CallCount); // permanent — không retry chính provider a
        Assert.Equal(1, b.CallCount);
    }

    [Fact]
    public async Task Retries_the_same_provider_once_on_a_transient_error_before_falling_over()
    {
        var a = new FakeAiProvider("a") { AlwaysThrow = new AiProviderTransientException("a", "tạm thời", 0.01) };
        var b = new FakeAiProvider("b") { NextResponse = "trả lời từ b" };
        var router = BuildRouter(a, b);

        var result = await router.CompleteTextAsync(SomeMessages, 100, CancellationToken.None);

        Assert.Equal("trả lời từ b", result);
        Assert.Equal(2, a.CallCount); // transient — 1 lần gốc + 1 lần retry rồi mới sang provider b
        Assert.Equal(1, b.CallCount);
    }

    [Fact]
    public async Task Succeeds_without_failover_when_the_first_provider_recovers_on_retry()
    {
        var a = new FakeAiProvider("a") { NextException = new AiProviderTransientException("a", "blip", 0.01) };
        a.NextResponse = "trả lời từ a sau khi hồi phục";
        var b = new FakeAiProvider("b") { NextResponse = "KHÔNG được gọi tới" };
        var router = BuildRouter(a, b);

        var result = await router.CompleteTextAsync(SomeMessages, 100, CancellationToken.None);

        Assert.Equal("trả lời từ a sau khi hồi phục", result);
        Assert.Equal(2, a.CallCount);
        Assert.Equal(0, b.CallCount); // a tự hồi phục — không cần chuyển provider
    }

    [Fact]
    public async Task Throws_AllAiProvidersFailedException_when_every_provider_is_exhausted()
    {
        var a = new FakeAiProvider("a") { AlwaysThrow = new AiProviderTransientException("a", "a lỗi", 0.01) };
        var b = new FakeAiProvider("b") { AlwaysThrow = new AiProviderPermanentException("b", "b lỗi vĩnh viễn") };
        var router = BuildRouter(a, b);

        var ex = await Assert.ThrowsAsync<AllAiProvidersFailedException>(
            () => router.CompleteTextAsync(SomeMessages, 100, CancellationToken.None));

        // Lỗi cuối cùng gặp phải (từ provider b, permanent) — không gợi ý retryAfterSeconds vì
        // provider b thất bại kiểu vĩnh viễn, dù provider a trước đó có retryAfterSeconds.
        Assert.Contains("b lỗi vĩnh viễn", ex.Message);
        Assert.Null(ex.RetryAfterSeconds);
    }

    [Fact]
    public async Task ChatAsync_calls_only_the_primary_provider_with_no_retry_or_failover()
    {
        // Quyết định 2026-08-18 — chat KHÔNG failover ở giai đoạn này (xem AiProviderRouter.ChatAsync).
        var a = new FakeAiProvider("a") { AlwaysThrow = new AiProviderTransientException("a", "lỗi", 0.01) };
        var b = new FakeAiProvider("b") { NextResponse = "KHÔNG được gọi tới" };
        var router = BuildRouter(a, b);

        await Assert.ThrowsAsync<AiProviderTransientException>(
            () => router.ChatAsync(SomeMessages, 100, CancellationToken.None));

        Assert.Equal(1, a.CallCount); // không retry
        Assert.Equal(0, b.CallCount); // không failover
    }
}
