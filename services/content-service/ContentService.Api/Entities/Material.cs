namespace ContentService.Api.Entities;

public sealed class Material
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; set; }
    public string? Chapter { get; set; }
    public string? Description { get; set; }
    public required string FileName { get; set; }
    public required string FileUrl { get; set; }
    public long FileSize { get; set; }

    /// <summary>Cloudinary public_id — needed to delete the actual file from Cloudinary when the
    /// material record is deleted. Null for materials that predate the Cloudinary migration.</summary>
    public string? CloudinaryPublicId { get; set; }

    /// <summary>auth-service user id of the uploader — a cross-service reference, not a live FK
    /// (auth-service and content-service each own their own schema).</summary>
    public required Guid UploadedBy { get; init; }

    public bool IsActive { get; set; } = true;
    public int ViewCount { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
