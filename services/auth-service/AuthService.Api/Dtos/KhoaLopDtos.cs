namespace AuthService.Api.Dtos;

public sealed record KhoaResponse(Guid Id, string Ten);

public sealed record CreateKhoaRequest(string Ten);

public sealed record UpdateKhoaRequest(string Ten);

// Việc V (2026-08-20, rà soát Lần II mục 1.6/1.9) — thêm NamHoc: field RIÊNG của Lớp (VD
// "2024-2025", năm tuyển sinh/thành lập lớp), KHÁC User.NamHoc (Việc 3.1 — năm học cá nhân từng học
// viên tự khai, có thể khác nhau giữa các em trong cùng 1 Lớp, không dùng làm nguồn cho cột này).
public sealed record LopResponse(Guid Id, string Ten, Guid KhoaId, Guid? GiaoVienId, string? NamHoc);

public sealed record CreateLopRequest(string Ten, Guid KhoaId, string? NamHoc = null);

// Rà soát Lần IX (2026-08-21) — thêm KhoaId (tùy chọn): null nghĩa là "không đổi", cùng quy ước
// NamHoc phía trên — modal "Sửa lớp" trước đây thiếu hẳn field Khóa nên GV/Admin không có cách nào
// chuyển 1 Lớp sang Khóa khác sau khi tạo.
public sealed record UpdateLopRequest(string Ten, string? NamHoc = null, Guid? KhoaId = null);

/// <summary>LopId = null nghĩa là gỡ học viên khỏi lớp hiện tại.</summary>
public sealed record AssignLopRequest(Guid? LopId);

/// <summary>GiaoVienId = null nghĩa là gỡ giáo viên chủ nhiệm khỏi lớp.</summary>
public sealed record AssignGiaoVienRequest(Guid? GiaoVienId);

public sealed record ChangeChucVuRequest(string ChucVu);

/// <summary>Việc V (2026-08-20) — Admin/GV chủ nhiệm sửa Cấp bậc của 1 học viên trực tiếp trong
/// roster (quyết định đã duyệt: cho sửa Cấp bậc, KHÔNG cho sửa Điểm vì đó là kết quả bài làm thật).
/// CapBac null/rỗng = "Chưa cập nhật", cùng quy ước PUT /auth/me (UpdateProfileRequest).</summary>
public sealed record ChangeCapBacRequest(string? CapBac);

/// <summary>Việc C (2026-08-16) — roster tối thiểu cho bảng xếp hạng theo Lớp, chỉ Id+Name+ChucVu
/// (KHÔNG Email như UserResponse) vì endpoint này service-to-service (quiz-service gọi, internal-
/// key gated) và có thể phục vụ cả Student xem lớp mình — không nên lộ email bạn học. ChucVu thêm ở
/// Việc IV (2026-08-20, rà soát Lần II mục 1.3) — không nhạy cảm như email, cần để hiện badge Chức
/// vụ trên Bảng xếp hạng. CapBac thêm ở Rà soát Lần III (2026-08-21, mục C) — cùng lý do, không
/// nhạy cảm như email, học viên khác trong lớp đã thấy nhau ở roster/lopDetailModal rồi. AvatarUrl
/// thêm ở Rà soát Lần V — chỉ là URL Cloudinary có sẵn (không phải file ảnh), không tốn thêm băng
/// thông/lưu trữ nào ngoài 1 chuỗi text, browser tự cache khi hiện lên bảng xếp hạng.</summary>
public sealed record HocVienIdResponse(Guid Id, string Name, string ChucVu, string? CapBac, string? AvatarUrl);

/// <summary>Gap 2 mục 3 — 1 dòng nhật ký hoạt động Lớp. ActorName/TargetName resolve sẵn (join
/// thẳng bảng Users cùng schema) để frontend không phải gọi thêm request lấy tên.</summary>
public sealed record LopActivityLogResponse(
    Guid Id,
    Guid LopId,
    Guid ActorUserId,
    string ActorName,
    string ActionType,
    Guid TargetUserId,
    string TargetName,
    string? OldValue,
    string? NewValue,
    DateTime CreatedAtUtc);

/// <summary>Việc 4.2 mục 3 (2026-08-19) — kết quả xóa toàn bộ dữ liệu 1 Lớp ở phía auth-service
/// (tài khoản học viên + nhật ký hoạt động + chính Lớp). Idempotent: gọi lại sau khi Lớp đã bị xóa
/// trả LopDeleted=false, UsersDeleted=0, ActivityLogsDeleted=0 (KHÔNG lỗi 404) — an toàn cho retry
/// sau 1 lần saga thất bại giữa chừng ở bước KHÁC (quiz/progress-service).</summary>
public sealed record DeleteAllLopDataResponse(int UsersDeleted, int ActivityLogsDeleted, bool LopDeleted);
