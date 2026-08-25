namespace AdminService.Api.Dtos;

// Việc V (2026-08-20, rà soát Lần II mục 1.6/1.9/1.11) — thêm CapBac: roster hợp nhất Admin
// (lopDetailModal) cần hiện + sửa Cấp bậc trực tiếp, trước đó DTO này cố ý tối giản (không có
// CapBac/SoDienThoai/NamHoc/BoMonKhoa vì "Quản lý tài khoản"/dropdown thêm học viên không cần) —
// chỉ thêm đúng field mới thật sự cần, không phình thêm các field còn lại chưa có use case.
// MonHocPhuTrach thêm ở Rà soát Lần VI (2026-08-21) — bảng "Quản lý tài khoản" cần hiện + Admin
// sửa trực tiếp môn học phụ trách của Giảng viên.
// Rà soát Lần XIII (2026-08-21) — thêm ChucVuGV: panel "Quản lý GV" mới cần hiện chức vụ chuyên
// môn (Giảng viên/Giảng viên chính/...) của từng GV, trước đây admin-service chưa từng cần field
// này (chỉ auth-service's UserResponse có).
// Rà soát Lần XV (2026-08-21) — thêm NamHoc: panel "Quản lý Tài khoản" tab Học viên (roster theo
// Lớp) cần hiện Năm học từng em, cùng lý do ChucVuGV ở trên (admin-service chưa từng cần).
public sealed record UserSummaryResponse(Guid Id, string Email, string Name, string Role, Guid? LopId = null, string ChucVu = "Học viên", string? CapBac = null, string? MonHocPhuTrach = null, bool IsLocked = false, string? ChucVuGV = null, string? NamHoc = null);

public sealed record ChangeRoleRequest(string Role);

/// <summary>Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp, không qua tự đăng ký.</summary>
public sealed record CreateUserRequest(string Email, string Password, string Name, string Role);

/// <summary>Rà soát Lần XII (2026-08-21) — khóa/mở khóa tài khoản.</summary>
public sealed record SetUserLockedRequest(bool IsLocked);

// Rà soát Lần XVII (2026-08-21) — thêm AdminName/TargetName resolve sẵn: audit log trước chỉ hiện
// Id thô (không đọc được ai đổi ai) — người dùng phản hồi cần "dễ quan sát" hơn, đặc biệt các thay
// đổi liên quan Giảng viên. Null nếu user đó không tìm thấy (đã bị xóa — hiện tại chưa có hard-
// delete nên hiếm khi xảy ra, nhưng vẫn xử lý an toàn).
public sealed record RoleChangeAuditResponse(Guid Id, Guid AdminUserId, string? AdminName, Guid TargetUserId, string? TargetName, string OldRole, string NewRole, DateTime ChangedAtUtc);

public sealed record SystemConfigResponse(string Key, string Value, DateTime UpdatedAtUtc);

public sealed record SetConfigRequest(string Value);

public sealed record SystemOverviewResponse(int TotalStudents, int TotalTeachers, int TotalAdmins, int TotalMaterials, int TotalQuestions, int TotalOralQuestions);

/// <summary>Việc 7 (2026-08-16) — Dashboard "Theo dõi Giáo viên". Danh sách phẳng, sắp xếp theo
/// tên (KHÔNG theo điểm/hiệu suất) — cố ý không phải bảng xếp hạng, chỉ liệt kê thông tin. AvgExam/
/// AvgPractice null nghĩa là học viên của giáo viên đó chưa có lượt làm bài nào loại đó.</summary>
public sealed record TeacherOverviewResponse(Guid TeacherId, string Name, int LopCount, int TotalStudents, decimal? AvgExamScore, decimal? AvgPracticeScore, int QuestionCount, int MaterialCount);

/// <summary>Số câu hỏi theo Chương (toàn hệ thống) — cho biểu đồ cột "Câu hỏi theo Chương".</summary>
public sealed record ChapterQuestionCountResponse(string Chapter, int Count);

/// <summary>Việc D (2026-08-16) — chi tiết TỪNG Lớp của 1 giáo viên (khác TeacherOverviewResponse
/// vốn gộp chung mọi Lớp thành 1 con số duy nhất, không thấy Lớp nào đang yếu). AvgExam/AvgPractice
/// null = Lớp đó chưa có học viên nào làm bài loại đó.</summary>
public sealed record LopQualityResponse(Guid LopId, string LopTen, int StudentCount, decimal? AvgExamScore, decimal? AvgPracticeScore);
