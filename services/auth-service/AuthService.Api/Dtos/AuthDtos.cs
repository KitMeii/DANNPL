namespace AuthService.Api.Dtos;

// Việc 3.1 (2026-08-19) — CapBac/SoDienThoai/NamHoc TÙY CHỌN, chỉ áp dụng cho Student (đăng ký
// luôn tạo Role=Student, xem AuthServiceImpl.RegisterAsync) — Teacher/Admin không có ở màn đăng ký.
public sealed record RegisterRequest(
    string Email, string Password, string Name,
    string? CapBac = null, string? SoDienThoai = null, string? NamHoc = null);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserResponse(
    Guid Id, string Email, string Name, string Role, Guid? LopId, string ChucVu, string? AvatarUrl = null,
    string? CapBac = null, string? SoDienThoai = null, string? NamHoc = null, string? BoMonKhoa = null,
    string? ChucVuGV = null, string? MonHocPhuTrach = null, bool IsLocked = false);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);

/// <summary>Minimal display info other services (progress-service leaderboard, admin-service
/// roster) enrich cross-service data with — name only, not email/role, to keep exposure minimal.</summary>
/// <summary>Role thêm ở Việc B (2026-08-16) — progress-service's leaderboard cần lọc bỏ tài
/// khoản Teacher/Admin (bug đã xác nhận: "Giáo viên Demo" lọt vào bảng xếp hạng vì StudentProgress
/// không phân biệt role). Không phải dữ liệu nhạy cảm hơn Name — endpoint này vốn đã mở cho mọi
/// user đã đăng nhập.</summary>
public sealed record UserNameResponse(Guid Id, string Name, string Role);

public sealed record ChangeRoleRequest(string Role);

/// <summary>LopId (gán lớp) và ChucVu (lớp trưởng/học viên) do Admin/GV chủ nhiệm sửa qua endpoint
/// riêng (Bước B), không phải self-service. CapBac/SoDienThoai/NamHoc/BoMonKhoa (Việc 3.1) LÀ self-
/// service nhưng áp dụng theo Role — service chỉ ghi field đúng vai trò (VD Teacher gửi CapBac sẽ
/// bị bỏ qua, không lỗi) — xem AuthServiceImpl.UpdateProfileAsync.</summary>
public sealed record UpdateProfileRequest(
    string Name, string? CapBac = null, string? SoDienThoai = null, string? NamHoc = null, string? BoMonKhoa = null,
    string? ChucVuGV = null);

/// <summary>Rà soát Lần VI (2026-08-21) — đổi mật khẩu tự-phục vụ (self-service), bắt buộc xác thực
/// mật khẩu HIỆN TẠI trước khi cho đổi (tránh 1 phiên đăng nhập bị chiếm/đang mở bị lợi dụng đổi mật
/// khẩu mà không cần biết mật khẩu cũ). Cùng ràng buộc độ dài với RegisterRequest.Password (8-128).</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Rà soát Lần VI (2026-08-21) — môn học phụ trách của Giảng viên, CHỈ Admin sửa được
/// (Admin là nhà quản lý chính phân công môn dạy, GV không tự chọn môn cho mình) — khác ChucVuGV/
/// BoMonKhoa/CapBac ở UpdateProfileRequest (đều tự sửa được). Text tự do, null/rỗng = xóa.</summary>
public sealed record ChangeMonHocPhuTrachRequest(string? MonHocPhuTrach);

/// <summary>Rà soát Lần XIV (2026-08-21) — panel "Quản lý GV" cần Admin sửa được Chức vụ chuyên môn
/// của GV trực tiếp (trước đây ChucVuGV CHỈ GV tự sửa qua Hồ sơ cá nhân, không có đường Admin can
/// thiệp — quyết định ban đầu ở Rà soát Lần VI). Người dùng yêu cầu Admin toàn quyền quản lý GV nên
/// mở thêm đường Admin-only này, KHÔNG bỏ đường tự sửa của GV (2 đường cùng tồn tại, giống CapBac).</summary>
public sealed record ChangeChucVuGvRequest(string? ChucVuGV);

/// <summary>Rà soát Lần XVIII (2026-08-22) — Admin sửa Họ tên + Năm học của BẤT KỲ user nào (trước
/// đây Name CHỈ tự sửa được qua UpdateProfileAsync — JWT-scoped, Admin không có cách đổi tên người
/// khác; NamHoc cũng vậy). Người dùng yêu cầu rà soát lại: "cột nào có thì phải có CRUD" — panel
/// Quản lý GV/Quản lý Tài khoản/roster Học viên đều hiện cột Họ tên/Năm học nhưng chưa sửa được.
/// NamHoc áp dụng bất kể role (giống CapBac/ChucVuGV — Admin toàn quyền, không strip theo role).</summary>
public sealed record AdminEditUserRequest(string Name, string? NamHoc);

/// <summary>Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp (bất kỳ Role nào, KHÁC
/// RegisterRequest luôn tạo Student) — không qua luồng tự đăng ký. Role phải thuộc Roles.All (xem
/// validator), Password cùng ràng buộc độ dài RegisterRequest.</summary>
public sealed record CreateUserByAdminRequest(string Email, string Password, string Name, string Role);

/// <summary>Rà soát Lần XII (2026-08-21) — khóa/mở khóa tài khoản (chặn đăng nhập, KHÔNG xóa dữ
/// liệu — xem User.IsLocked remarks). Admin không tự khóa được chính mình (kiểm ở endpoint, cùng
/// nguyên tắc "không thể tự đổi role của chính mình" ở ChangeRoleRequest).</summary>
public sealed record SetUserLockedRequest(bool IsLocked);

/// <summary>Gap 2 mục 2 (Hướng B) — kết quả tìm học viên theo email để GV chủ nhiệm gán vào lớp
/// mình. CHỈ trả tối thiểu cần thiết cho việc đó: không có Role, ChucVu, hay bất kỳ field nào khác
/// của User — LopId hiện tại (nullable) chỉ để hiện rõ "học viên đang ở lớp khác" trước khi gán,
/// tránh gán nhầm, không phải để lộ thêm thông tin.</summary>
public sealed record StudentSearchResponse(Guid Id, string Name, string Email, Guid? LopId);
