using AuthService.Api.Storage;

namespace AuthService.Tests.Integration;

/// <summary>Swaps out CloudinaryAvatarStorage in tests — no real Cloudinary account/credentials
/// needed (same convention as content-service's FakeFileStorage).</summary>
public sealed class FakeAvatarStorage : IAvatarStorage
{
    public int UploadCallCount { get; private set; }
    public string? LastDeletedPublicId { get; private set; }
    public int DeleteCallCount { get; private set; }

    public Task<UploadedAvatar> UploadAsync(Stream content, string fileName, CancellationToken ct)
    {
        UploadCallCount++;
        using var memoryStream = new MemoryStream();
        content.CopyTo(memoryStream);
        return Task.FromResult(new UploadedAvatar(
            Url: $"https://res.cloudinary.com/fake/image/upload/tthcm/avatars/{fileName}",
            PublicId: $"tthcm/avatars/{Guid.NewGuid()}"));
    }

    public Task DeleteAsync(string publicId, CancellationToken ct)
    {
        DeleteCallCount++;
        LastDeletedPublicId = publicId;
        return Task.CompletedTask;
    }
}
