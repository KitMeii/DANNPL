namespace AuthService.Api.Storage;

/// <summary>Dùng lại đúng 1 Cloudinary account platform-owned với content-service (cùng
/// CloudName/ApiKey/ApiSecret, chỉ khác thư mục lưu — xem CloudinaryAvatarStorage remarks), không
/// cần secret riêng.</summary>
public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public required string CloudName { get; init; }
    public required string ApiKey { get; init; }
    public required string ApiSecret { get; init; }
}
