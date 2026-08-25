namespace AuthService.Api.Entities;

/// <summary>Lớp thuộc 1 Khóa (KhoaId), có tối đa 1 giáo viên chủ nhiệm (GiaoVienId, nullable —
/// null nghĩa là chưa gán). Học viên thuộc lớp qua User.LopId.</summary>
public sealed class Lop
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Ten { get; set; }
    public required Guid KhoaId { get; set; }
    public Guid? GiaoVienId { get; set; }

    /// <summary>Việc V (2026-08-20) — năm học/tuyển sinh của Lớp (VD "2024-2025"), tùy chọn. KHÁC
    /// User.NamHoc (cá nhân, Việc 3.1) — đây là thuộc tính của chính Lớp.</summary>
    public string? NamHoc { get; set; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
