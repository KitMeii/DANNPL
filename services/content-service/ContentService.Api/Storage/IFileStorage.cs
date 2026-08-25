namespace ContentService.Api.Storage;

public sealed record UploadedFile(string Url, string PublicId, long FileSize);

/// <summary>Abstraction over the raw-file storage backend (Cloudinary in production/dev; a fake
/// in tests, see FakeFileStorage) — keeps MaterialService and the upload endpoint decoupled from
/// the Cloudinary SDK directly.</summary>
public interface IFileStorage
{
    Task<UploadedFile> UploadAsync(Stream content, string fileName, CancellationToken ct);

    /// <summary>Best-effort — callers should not fail the overall operation if this throws.</summary>
    Task DeleteAsync(string publicId, CancellationToken ct);
}
