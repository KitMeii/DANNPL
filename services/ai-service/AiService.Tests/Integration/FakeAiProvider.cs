using AiService.Api.AiProviders;
using AiService.Api.AiProviders.Groq;

namespace AiService.Tests.Integration;

/// <summary>Stands in for a real IAiProvider in tests. Set <see cref="NextResponse"/> before each
/// call to control what "the model" returns, and read <see cref="CallCount"/> to assert caching/
/// retry behavior. Two ways to inject a failure: <see cref="NextException"/> throws ONCE then
/// clears itself (simulates "fails once, then recovers" — e.g. a single transient retry
/// succeeding), <see cref="AlwaysThrow"/> throws on EVERY call (simulates a provider that's
/// completely down — e.g. to prove AiProviderRouter falls over to the next provider, or exhausts
/// retries and surfaces AllAiProvidersFailedException).</summary>
public sealed class FakeAiProvider(string name = "fake") : IAiProvider
{
    public string Name { get; } = name;
    public string NextResponse { get; set; } = "OK";
    public int CallCount { get; private set; }
    public IReadOnlyList<AiMessage>? LastMessages { get; private set; }
    public int LastMaxTokens { get; private set; }
    public Exception? NextException { get; set; }
    public Exception? AlwaysThrow { get; set; }

    public Task<string> ChatAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct) =>
        CompleteAsync(messages, maxTokens);

    public Task<string> CompleteTextAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct) =>
        CompleteAsync(messages, maxTokens);

    // Applies the exact same cleanup GroqProvider.CompleteJsonAsync does — this fake stands in for
    // "a working IAiProvider", and the CompleteJsonAsync contract is "always returns clean JSON", so
    // tests that set NextResponse to a fenced/messy string (mimicking a real observed Groq quirk)
    // still exercise that guarantee instead of silently bypassing it.
    public Task<string> CompleteJsonAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct)
    {
        var raw = CompleteAsyncCore(messages, maxTokens);
        return Task.FromResult(MarkdownJson.ExtractFirstJsonValue(MarkdownJson.StripCodeFence(raw.Trim())));
    }

    private Task<string> CompleteAsync(IReadOnlyList<AiMessage> messages, int maxTokens) =>
        Task.FromResult(CompleteAsyncCore(messages, maxTokens));

    private string CompleteAsyncCore(IReadOnlyList<AiMessage> messages, int maxTokens)
    {
        CallCount++;
        LastMessages = messages;
        LastMaxTokens = maxTokens;

        if (AlwaysThrow is { } persistent)
        {
            throw persistent;
        }

        if (NextException is { } ex)
        {
            NextException = null;
            throw ex;
        }

        return NextResponse;
    }
}
