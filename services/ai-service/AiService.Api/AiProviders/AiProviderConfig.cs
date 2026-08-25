namespace AiService.Api.AiProviders;

public sealed class AiProvidersOptions
{
    public const string SectionName = "Ai";

    public List<AiProviderConfig> Providers { get; init; } = [];
}

/// <summary>
/// One entry in appsettings.json's Ai:Providers list. The API key itself is NEVER written here —
/// ApiKeyEnvVar only names which environment variable AiProviderFactory should read at startup
/// (same principle as the old Groq:ApiKey binding, just resolved explicitly instead of via .NET's
/// implicit env-var config provider), so secrets stay out of source control either way.
/// </summary>
public sealed class AiProviderConfig
{
    /// <summary>Must match a case handled in AiProviderFactory's provider-construction switch.</summary>
    public required string Name { get; init; }

    public bool Enabled { get; init; } = true;

    /// <summary>Lower tries first. AiProviderRouter iterates Enabled entries in ascending order.</summary>
    public int Priority { get; init; } = 1;

    public required string Model { get; init; }
    public required string BaseUrl { get; init; }
    public required string ApiKeyEnvVar { get; init; }
}
