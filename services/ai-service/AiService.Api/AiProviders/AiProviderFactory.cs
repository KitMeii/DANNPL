using AiService.Api.AiProviders.Groq;
using Microsoft.Extensions.Options;

namespace AiService.Api.AiProviders;

/// <summary>
/// Builds one IAiProvider instance per enabled entry in Ai:Providers (config), resolving each
/// provider's real API key from the environment variable its config entry names. Built once at
/// startup (registered as a singleton) — providers are stateless aside from their HttpClient, so
/// there's no reason to rebuild them per request.
///
/// To add a new provider: see AiProviders/README.md.
/// </summary>
public sealed class AiProviderFactory : IAiProviderFactory
{
    private readonly IReadOnlyList<IAiProvider> _providers;

    public AiProviderFactory(IOptions<AiProvidersOptions> options, IHttpClientFactory httpClientFactory)
    {
        _providers = options.Value.Providers
            .Where(config => config.Enabled)
            .OrderBy(config => config.Priority)
            .Select(config => Build(config, httpClientFactory))
            .ToList();
    }

    public IReadOnlyList<IAiProvider> GetProvidersByPriority() => _providers;

    private static IAiProvider Build(AiProviderConfig config, IHttpClientFactory httpClientFactory)
    {
        var apiKey = Environment.GetEnvironmentVariable(config.ApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"AI provider '{config.Name}' is enabled but environment variable '{config.ApiKeyEnvVar}' is not set.");
        }

        return config.Name switch
        {
            "groq" => new GroqProvider(httpClientFactory.CreateClient(nameof(GroqProvider)), config, apiKey),
            _ => throw new InvalidOperationException(
                $"No IAiProvider implementation registered for provider name '{config.Name}' — see AiProviders/README.md."),
        };
    }
}
