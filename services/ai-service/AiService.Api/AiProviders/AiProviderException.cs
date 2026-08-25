namespace AiService.Api.AiProviders;

/// <summary>
/// Base for every error an <see cref="IAiProvider"/> can throw. AiProviderRouter only ever sees
/// these two shapes — it never needs to know Groq's specific HTTP status codes or error text, that
/// translation happens once, inside each provider (see GroqProvider).
/// </summary>
public abstract class AiProviderException(string providerName, string message) : Exception(message)
{
    public string ProviderName { get; } = providerName;
}

/// <summary>Worth retrying — on this SAME provider first (rate limit, outage, network blip), then
/// falling over to the next configured provider if retries are exhausted.</summary>
public sealed class AiProviderTransientException(string providerName, string message, double? retryAfterSeconds)
    : AiProviderException(providerName, message)
{
    public double? RetryAfterSeconds { get; } = retryAfterSeconds;
}

/// <summary>Retrying THIS provider for THIS exact request is pointless (payload too large for its
/// limits, model rejected, malformed response) — skip straight to the next configured provider
/// instead of burning a retry here. A different vendor may have a larger context window or no such
/// restriction, so this still does NOT stop the router from trying the rest of the list.</summary>
public sealed class AiProviderPermanentException(string providerName, string message)
    : AiProviderException(providerName, message);

/// <summary>Every configured provider was tried (with retries) and none succeeded. Carries the last
/// RetryAfterSeconds seen (if the final failure was itself transient) so AiEndpoints can still tell
/// the caller whether waiting and retrying the whole request might help.</summary>
public sealed class AllAiProvidersFailedException(string message, double? retryAfterSeconds) : Exception(message)
{
    public double? RetryAfterSeconds { get; } = retryAfterSeconds;
}
