namespace AiService.Api.AiProviders;

/// <summary>
/// Retry + failover orchestration, shared by every AI feature EXCEPT chat (see ChatAsync remarks).
/// This is deliberately the ONLY place that knows about "try again", "give up on this provider,
/// try the next one" — individual IAiProvider implementations only classify their own errors as
/// transient/permanent (see AiProviderException.cs), they never loop or wait themselves. Adding a
/// second provider later needs zero changes here — it's already provider-count-agnostic.
/// </summary>
public sealed class AiProviderRouter(IAiProviderFactory providerFactory, ILogger<AiProviderRouter> logger)
    : IAiProviderRouter
{
    // Chunked lecture generation (LectureService) already paces itself ~45s between calls
    // client-side (see giang-bai.html LECTURE_CHUNK_PACE_MS remarks) — a transient error reaching
    // this router in practice means that pacing wasn't enough, which real end-to-end testing
    // (2026-08-18, 30-page PDF) never actually triggered. One extra in-request retry is a bounded
    // safety net on top of that, not the primary defense — kept small so a single HTTP request
    // can't balloon in latency waiting on a provider's own retry-after hint (which can be 30-40s+).
    private const int MaxRetriesPerProvider = 1;
    private static readonly TimeSpan DefaultBackoffWhenProviderGivesNoHint = TimeSpan.FromSeconds(3);

    /// <summary>
    /// NOT run through TryWithFailoverAsync — chat goes straight to the highest-priority provider,
    /// no retry, no fallover, at this stage (decision 2026-08-18). Reasons: a chat reply the user
    /// is mid-conversation with is cheap to just resend by hand if it fails, unlike a generated
    /// lecture/exam set the user would otherwise lose; and failing over mid-turn would get more
    /// confusing once chat gains streaming (a partially-streamed reply from provider A can't cleanly
    /// hand off to provider B). IAiProvider.ChatAsync exists on every provider regardless, so
    /// enabling failover for chat later is a one-line change to THIS method's body — swap the
    /// direct call below for `TryWithFailoverAsync(p => p.ChatAsync(...), ct)` — no interface or
    /// caller changes needed.
    /// </summary>
    public Task<string> ChatAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct)
    {
        var provider = GetPrimaryProvider();
        return provider.ChatAsync(messages, maxTokens, ct);
    }

    public Task<string> CompleteTextAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct) =>
        TryWithFailoverAsync(provider => provider.CompleteTextAsync(messages, maxTokens, ct), ct);

    public Task<string> CompleteJsonAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct) =>
        TryWithFailoverAsync(provider => provider.CompleteJsonAsync(messages, maxTokens, ct), ct);

    private IAiProvider GetPrimaryProvider()
    {
        var providers = providerFactory.GetProvidersByPriority();
        if (providers.Count == 0)
        {
            throw new InvalidOperationException("No AI provider is configured (Ai:Providers is empty).");
        }
        return providers[0];
    }

    private async Task<string> TryWithFailoverAsync(Func<IAiProvider, Task<string>> call, CancellationToken ct)
    {
        var providers = providerFactory.GetProvidersByPriority();
        if (providers.Count == 0)
        {
            throw new InvalidOperationException("No AI provider is configured (Ai:Providers is empty).");
        }

        string? lastMessage = null;
        double? lastRetryAfterSeconds = null;

        foreach (var provider in providers)
        {
            for (var attempt = 0; attempt <= MaxRetriesPerProvider; attempt++)
            {
                try
                {
                    return await call(provider);
                }
                catch (AiProviderTransientException ex)
                {
                    lastMessage = ex.Message;
                    lastRetryAfterSeconds = ex.RetryAfterSeconds;
                    logger.LogWarning(
                        "AI provider {Provider} transient error (attempt {Attempt}/{Max}): {Message}",
                        provider.Name, attempt + 1, MaxRetriesPerProvider + 1, ex.Message);

                    if (attempt < MaxRetriesPerProvider)
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(ex.RetryAfterSeconds ?? DefaultBackoffWhenProviderGivesNoHint.TotalSeconds), ct);
                        continue;
                    }
                }
                catch (AiProviderPermanentException ex)
                {
                    lastMessage = ex.Message;
                    lastRetryAfterSeconds = null; // retrying THIS provider is pointless — don't imply otherwise to the caller
                    logger.LogWarning("AI provider {Provider} permanent error: {Message}", provider.Name, ex.Message);
                }

                break; // fall through to the next provider in priority order
            }
        }

        throw new AllAiProvidersFailedException(
            lastMessage ?? "No AI provider produced a response.", lastRetryAfterSeconds);
    }
}
