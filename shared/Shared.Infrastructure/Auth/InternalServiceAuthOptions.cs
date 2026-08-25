namespace Shared.Infrastructure.Auth;

/// <summary>Shared secret used to gate endpoints that must only ever be called service-to-service
/// (see <see cref="RequireInternalServiceKeyFilter"/> for why an [Authorize(Roles=Admin)] JWT
/// check alone isn't enough for those).</summary>
public sealed class InternalServiceAuthOptions
{
    public const string SectionName = "InternalService";
    public string SharedKey { get; init; } = "";
}
