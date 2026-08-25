namespace AuthService.Api.Storage;

public sealed record UploadedAvatar(string Url, string PublicId);

/// <summary>Abstraction over the avatar storage backend (Cloudinary in production/dev; a fake in
/// tests) — same convention as content-service's IFileStorage, kept separate (not shared) because
/// avatar (User, image, 1-1 owner) and lecture materials (Material, raw docs, many-per-service) are
/// different domains owned by different services.</summary>
public interface IAvatarStorage
{
    Task<UploadedAvatar> UploadAsync(Stream content, string fileName, CancellationToken ct);

    /// <summary>Best-effort — callers should not fail the overall operation if this throws.</summary>
    Task DeleteAsync(string publicId, CancellationToken ct);
}
