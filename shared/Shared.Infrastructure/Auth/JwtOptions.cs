namespace Shared.Infrastructure.Auth;

/// <summary>Bound from config section "Jwt". SigningKey must come from an environment variable
/// (Jwt__SigningKey) in every environment — never hardcode it in appsettings.json.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public int AccessTokenMinutes { get; init; } = 60;
}
