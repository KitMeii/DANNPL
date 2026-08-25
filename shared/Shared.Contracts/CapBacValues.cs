namespace Shared.Contracts;

/// <summary>Cấp bậc quân hàm QĐND Việt Nam — trường tùy chọn, học viên tự điền lúc đăng ký hoặc
/// sau ở Hồ sơ cá nhân. Danh sách CỐ ĐỊNH (không cho text tự do) để thống kê/lọc sau này chính xác,
/// tránh sai chính tả. KHÔNG liên quan tới ChucVuValues (AuthService.Api.Entities) — đó là vai trò
/// trong lớp học (Lớp trưởng/phó), một khái niệm hoàn toàn khác.</summary>
public static class CapBacValues
{
    public const string ChuaCapNhat = "Chưa cập nhật";

    // Hạ sĩ quan / binh sĩ
    public const string BinhNhi = "Binh nhì";
    public const string BinhNhat = "Binh nhất";
    public const string HaSi = "Hạ sĩ";
    public const string TrungSi = "Trung sĩ";
    public const string ThuongSi = "Thượng sĩ";

    // Sĩ quan cấp úy
    public const string ThieuUy = "Thiếu úy";
    public const string TrungUy = "Trung úy";
    public const string ThuongUy = "Thượng úy";
    public const string DaiUy = "Đại úy";

    // Sĩ quan cấp tá
    public const string ThieuTa = "Thiếu tá";
    public const string TrungTa = "Trung tá";
    public const string ThuongTa = "Thượng tá";
    public const string DaiTa = "Đại tá";

    public static readonly string[] All =
    [
        ChuaCapNhat,
        BinhNhi, BinhNhat, HaSi, TrungSi, ThuongSi,
        ThieuUy, TrungUy, ThuongUy, DaiUy,
        ThieuTa, TrungTa, ThuongTa, DaiTa,
    ];
}
