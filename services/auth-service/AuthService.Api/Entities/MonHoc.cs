namespace AuthService.Api.Entities;

/// <summary>Rà soát Lần XVI (2026-08-21) — Môn học/học phần (Tên, Tín chỉ, GV đảm nhiệm), khái
/// niệm MỚI hoàn toàn — KHÁC User.MonHocPhuTrach (text tự do GV tự khai, không có Tín chỉ, không
/// gắn Lớp cụ thể nào). GiaoVienId nullable (chưa gán GV). Lớp đang học môn này là quan hệ nhiều-
/// nhiều — xem MonHocLop.</summary>
public sealed class MonHoc
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Ten { get; set; }
    public required string MaHocPhan { get; set; }
    public required int TinChi { get; set; }
    public Guid? GiaoVienId { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
