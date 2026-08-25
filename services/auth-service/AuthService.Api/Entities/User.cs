using Shared.Contracts;

namespace AuthService.Api.Entities;

public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Email { get; init; }
    public required string Name { get; set; }
    public required string PasswordHash { get; set; }
    public string Role { get; set; } = Roles.Student;
    public Guid? LopId { get; set; }
    public string ChucVu { get; set; } = ChucVuValues.HocVien;
    public string? AvatarUrl { get; set; }
    /// <summary>Cloudinary public_id — cần để xóa ảnh cũ khỏi Cloudinary khi đổi avatar mới. Null
    /// nếu user chưa từng tải avatar (cùng pattern Material.CloudinaryPublicId).</summary>
    public string? AvatarPublicId { get; set; }

    // Việc 3.1 (2026-08-19) — mở rộng thông tin cá nhân, tất cả TÙY CHỌN (nullable), additive. Học
    // viên tự điền CapBac/SoDienThoai/NamHoc lúc đăng ký hoặc sau ở Hồ sơ cá nhân; BoMonKhoa chỉ áp
    // dụng cho Teacher. KHÔNG liên quan ChucVu (vai trò trong lớp: Lớp trưởng/phó) — giữ tách biệt.
    /// <summary>Cấp bậc quân hàm — PHẢI thuộc Shared.Contracts.CapBacValues.All nếu có giá trị (xem
    /// UpdateProfileRequestValidator/RegisterRequestValidator), không phải text tự do.</summary>
    public string? CapBac { get; set; }
    public string? SoDienThoai { get; set; }
    /// <summary>Định dạng "YYYY-YYYY" (VD "2025-2026"), năm sau = năm trước + 1.</summary>
    public string? NamHoc { get; set; }
    /// <summary>Bộ môn/Khoa của giảng viên — text tự do (KHÔNG dùng entity Khoa đã có, entity đó
    /// là "khóa học theo năm nhập học" của HỌC VIÊN, nghĩa hoàn toàn khác dù trùng chữ "Khoa").</summary>
    public string? BoMonKhoa { get; set; }

    /// <summary>Rà soát Lần VI (2026-08-21) — chức vụ chuyên môn của GIẢNG VIÊN (Giảng viên/Giảng
    /// viên chính/Trưởng bộ môn/...), PHẢI thuộc ChucVuGvValues.All nếu có giá trị — KHÔNG liên
    /// quan ChucVu ở trên (vai trò của HỌC VIÊN trong lớp: Lớp trưởng/phó). Tự sửa được (giống
    /// BoMonKhoa) vì đây là chức danh học thuật GV tự khai, không phải phân công của Admin.</summary>
    public string? ChucVuGV { get; set; }

    /// <summary>Rà soát Lần VI (2026-08-21) — môn học GV phụ trách, text tự do, do ADMIN chỉ định
    /// (Admin là nhà quản lý chính phân công môn dạy) — KHÁC BoMonKhoa/ChucVuGV ở trên (GV tự sửa
    /// được). Xem ChangeMonHocPhuTrachAsync (KhoaLopService) — Admin-only.</summary>
    public string? MonHocPhuTrach { get; set; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Rà soát Lần XII (2026-08-21) — Admin khóa/mở khóa tài khoản thay vì xóa hẳn (giữ dữ
    /// liệu liên quan — Lớp/điểm/câu hỏi đã tạo — toàn vẹn, chỉ chặn đăng nhập). Kiểm ở
    /// AuthServiceImpl.LoginAsync; KHÔNG hủy JWT đang có hiệu lực của phiên đăng nhập trước đó (out
    /// of scope, cùng giới hạn với đổi mật khẩu/đổi role hiện tại — access token ngắn hạn tự hết
    /// hạn).</summary>
    public bool IsLocked { get; set; }
}
