namespace ContentService.Api.Storage;

/// <summary>Platform-owned Cloudinary credentials, configured via env vars (Cloudinary__CloudName
/// etc.) — never exposed to the browser. Replaces the old client-side Supabase Storage upload
/// (admin.html used to hold the Supabase anon key + upload directly from the browser).</summary>
public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public required string CloudName { get; init; }
    public required string ApiKey { get; init; }
    public required string ApiSecret { get; init; }
}
