namespace AiService.Api.AiProviders;

/// <summary>
/// One AI vendor's transport + response-shaping. A provider owns HOW to talk to its API and how to
/// turn its response into (a) free-form text or (b) a clean JSON string — it does NOT own business
/// prompts/schemas (those stay in the calling Service, e.g. OralGradingService), and it does NOT own
/// retry/failover orchestration (that's AiProviderRouter's job — see remarks there). Throw
/// AiProviderTransientException/AiProviderPermanentException for anything that goes wrong; never let
/// a vendor-specific exception type (e.g. HttpRequestException) escape a provider implementation.
///
/// To add a new provider: see AiProviders/README.md.
/// </summary>
public interface IAiProvider
{
    /// <summary>Matches the "Name" configured in appsettings.json's Ai:Providers entries.</summary>
    string Name { get; }

    /// <summary>Multi-turn chat completion. Called DIRECTLY (bypassing AiProviderRouter's
    /// retry/failover) by ChatService today — see AiProviderRouter.ChatAsync remarks for why.</summary>
    Task<string> ChatAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct);

    /// <summary>Free-form text completion (lecture narration, comprehension questions).</summary>
    Task<string> CompleteTextAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct);

    /// <summary>Same as CompleteTextAsync, but the returned string is GUARANTEED to be valid JSON,
    /// ready for JsonSerializer.Deserialize with no further cleanup by the caller. Each provider
    /// decides how it gets there — GroqProvider still relies on prompt-engineering (the caller's
    /// prompt asks for JSON) plus stripping markdown code fences/trailing text; a provider with a
    /// native structured-output API could just pass that flag and return content unmodified.</summary>
    Task<string> CompleteJsonAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct);
}
