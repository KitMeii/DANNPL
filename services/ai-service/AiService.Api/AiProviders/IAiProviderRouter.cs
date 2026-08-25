namespace AiService.Api.AiProviders;

/// <summary>Provider-agnostic entry point every business Service calls instead of talking to a
/// specific IAiProvider directly. See AiProviderRouter for what each method actually does.</summary>
public interface IAiProviderRouter
{
    Task<string> ChatAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct);
    Task<string> CompleteTextAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct);
    Task<string> CompleteJsonAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct);
}
