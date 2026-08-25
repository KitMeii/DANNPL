using AiService.Api.AiProviders;

namespace AiService.Tests.Integration;

/// <summary>Test-only IAiProviderFactory — returns exactly the providers it was constructed with,
/// in the order given (AiProviderRouter treats index 0 as highest priority).</summary>
public sealed class ListAiProviderFactory(IReadOnlyList<IAiProvider> providers) : IAiProviderFactory
{
    public IReadOnlyList<IAiProvider> GetProvidersByPriority() => providers;
}
