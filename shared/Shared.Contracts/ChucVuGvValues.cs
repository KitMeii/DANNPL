namespace Shared.Contracts;

/// <summary>Rà soát Lần VI (2026-08-21) — chức vụ chuyên môn của GIẢNG VIÊN, trường tùy chọn, GV tự
/// điền ở Hồ sơ cá nhân. Danh sách CỐ ĐỊNH (không cho text tự do) cùng lý do với CapBacValues —
/// thống kê/lọc sau này chính xác, tránh sai chính tả. KHÔNG liên quan ChucVuValues (vai trò của
/// HỌC VIÊN trong lớp: Lớp trưởng/phó) hay CapBacValues (cấp bậc quân hàm, GV dùng chung field đó).</summary>
public static class ChucVuGvValues
{
    public const string ChuaCapNhat = "Chưa cập nhật";
    public const string GiangVien = "Giảng viên";
    public const string GiangVienChinh = "Giảng viên chính";
    public const string GiangVienCaoCap = "Giảng viên cao cấp";
    public const string PhoTruongBoMon = "Phó trưởng bộ môn";
    public const string TruongBoMon = "Trưởng bộ môn";
    public const string PhoTruongKhoa = "Phó trưởng khoa";
    public const string TruongKhoa = "Trưởng khoa";

    public static readonly string[] All =
    [
        ChuaCapNhat,
        GiangVien, GiangVienChinh, GiangVienCaoCap,
        PhoTruongBoMon, TruongBoMon,
        PhoTruongKhoa, TruongKhoa,
    ];
}
