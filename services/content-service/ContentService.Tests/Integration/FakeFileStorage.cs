using ContentService.Api.Storage;

namespace ContentService.Tests.Integration;

/// <summary>Swaps out CloudinaryFileStorage in tests — no real Cloudinary account/credentials
/// needed to run the test suite (same convention as FakeGroqClient, FakeOralGradingClient, etc.
/// elsewhere in this codebase).</summary>
public sealed class FakeFileStorage : IFileStorage
{
    public int UploadCallCount { get; private set; }
    public string? LastDeletedPublicId { get; private set; }

    public Task<UploadedFile> UploadAsync(Stream content, string fileName, CancellationToken ct)
    {
        UploadCallCount++;
        using var memoryStream = new MemoryStream();
        content.CopyTo(memoryStream);
        return Task.FromResult(new UploadedFile(
            Url: $"https://res.cloudinary.com/fake/raw/upload/tthcm/materials/{fileName}",
            PublicId: $"tthcm/materials/{Guid.NewGuid()}",
            FileSize: memoryStream.Length));
    }

    public Task DeleteAsync(string publicId, CancellationToken ct)
    {
        LastDeletedPublicId = publicId;
        return Task.CompletedTask;
    }
}
