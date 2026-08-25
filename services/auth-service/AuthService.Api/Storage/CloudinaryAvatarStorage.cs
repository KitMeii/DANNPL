using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace AuthService.Api.Storage;

/// <summary>Uploads/deletes user avatars server-side (browser never sees Cloudinary credentials —
/// same convention as content-service's CloudinaryFileStorage). Resource type "image" (not "raw"
/// like Material) so Cloudinary can transform it: crop to a 256×256 square centered on the detected
/// face, keeping stored avatars small and consistently shaped regardless of what the user
/// uploads.</summary>
public sealed class CloudinaryAvatarStorage : IAvatarStorage
{
    private const string AvatarsFolder = "nnpl/avatars";
    private readonly Cloudinary _cloudinary;

    public CloudinaryAvatarStorage(IOptions<CloudinaryOptions> options)
    {
        var o = options.Value;
        _cloudinary = new Cloudinary(new Account(o.CloudName, o.ApiKey, o.ApiSecret))
        {
            Api = { Secure = true },
        };
    }

    public async Task<UploadedAvatar> UploadAsync(Stream content, string fileName, CancellationToken ct)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, content),
            Folder = AvatarsFolder,
            UseFilename = true,
            UniqueFilename = true,
            Transformation = new Transformation().Width(256).Height(256).Crop("fill").Gravity("face"),
        };

        var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken: ct);

        if (result.Error is not null)
        {
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
        }

        return new UploadedAvatar(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteAsync(string publicId, CancellationToken ct)
    {
        var deleteParams = new DeletionParams(publicId) { ResourceType = ResourceType.Image };
        await _cloudinary.DestroyAsync(deleteParams);
    }
}
