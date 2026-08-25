namespace AiService.Api.AiProviders;

public interface IAiProviderFactory
{
    /// <summary>Enabled providers only, ascending Priority (index 0 = tried first).</summary>
    IReadOnlyList<IAiProvider> GetProvidersByPriority();
}
