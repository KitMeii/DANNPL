namespace AuthService.Api.Entities;

/// <summary>Khóa học theo năm nhập học (vd "K59"). Chứa nhiều Lớp — xem Lop.KhoaId.</summary>
public sealed class Khoa
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Ten { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
