using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace ContentService.Api.Storage;

/// <summary>Uploads/deletes lecture material files (PDFs — non-image, so Cloudinary's "raw"
/// resource type) server-side. The browser never talks to Cloudinary directly and never sees the
/// API key/secret — it POSTs the file to this service, which is the only thing holding the
/// credentials, same convention as ai-service holding the one platform Groq key.</summary>
public sealed class CloudinaryFileStorage : IFileStorage
{
    private const string MaterialsFolder = "nnpl/materials";
    private readonly Cloudinary _cloudinary;

    public CloudinaryFileStorage(IOptions<CloudinaryOptions> options)
    {
        var o = options.Value;
        _cloudinary = new Cloudinary(new Account(o.CloudName, o.ApiKey, o.ApiSecret))
        {
            Api = { Secure = true },
        };
    }

    public async Task<UploadedFile> UploadAsync(Stream content, string fileName, CancellationToken ct)
    {
        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(fileName, content),
            Folder = MaterialsFolder,
            UseFilename = true,
            UniqueFilename = true,
        };

        var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken: ct);

        if (result.Error is not null)
        {
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
        }

        return new UploadedFile(result.SecureUrl.ToString(), result.PublicId, result.Bytes);
    }

    public async Task DeleteAsync(string publicId, CancellationToken ct)
    {
        var deleteParams = new DeletionParams(publicId) { ResourceType = ResourceType.Raw };
        await _cloudinary.DestroyAsync(deleteParams);
    }
}
