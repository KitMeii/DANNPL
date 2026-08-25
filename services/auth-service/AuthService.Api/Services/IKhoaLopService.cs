using AuthService.Api.Dtos;

namespace AuthService.Api.Services;

public interface IKhoaLopService
{
    Task<KhoaResponse> CreateKhoaAsync(CreateKhoaRequest request, CancellationToken ct);
    Task<IReadOnlyList<KhoaResponse>> ListKhoaAsync(CancellationToken ct);
    Task<KhoaResponse> GetKhoaByIdAsync(Guid id, CancellationToken ct);
    Task<KhoaResponse> UpdateKhoaAsync(Guid id, UpdateKhoaRequest request, CancellationToken ct);
    Task DeleteKhoaAsync(Guid id, CancellationToken ct);

    /// <summary>Việc 4.2 (2026-08-19) — callerUserId/callerRole = người gọi. Teacher tạo Lớp mới TỰ
    /// ĐỘNG trở thành GV chủ nhiệm (GiaoVienId = callerUserId) — hợp lý hơn để trống rồi bắt Admin
    /// gán tay thêm 1 bước. Admin tạo thì GiaoVienId để trống như trước, gán sau qua
    /// AssignGiaoVienAsync. Tạo Khóa mới vẫn Admin-only, không đổi.</summary>
    Task<LopResponse> CreateLopAsync(CreateLopRequest request, Guid callerUserId, string callerRole, CancellationToken ct);
    Task<IReadOnlyList<LopResponse>> ListLopAsync(Guid? khoaId, CancellationToken ct);
    Task<LopResponse> GetLopByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Việc 4.2 — Admin luôn được; Teacher chỉ được nếu đúng là GiaoVienId của lớp này (cùng
    /// pattern ownership check data-dependent với ChangeChucVuAsync).</summary>
    Task<LopResponse> UpdateLopAsync(Guid id, UpdateLopRequest request, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Việc 4.2 — Admin luôn được; Teacher chỉ được nếu đúng là GiaoVienId của lớp này. Guard
    /// "chặn nếu còn học viên" giữ nguyên như trước (KHÔNG phải chức năng "xóa toàn bộ dữ liệu Lớp" —
    /// đó là 1 tính năng riêng, Admin-only, xem AdminService khi implement Việc 4.2 mục 3).</summary>
    Task DeleteLopAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>giaoVienId luôn lấy từ JWT của người gọi (endpoint không nhận tham số) — GV chỉ có
    /// thể liệt kê đúng lớp mình phụ trách, không có cách nào truyền id khác để dò lớp của GV khác.</summary>
    Task<IReadOnlyList<LopResponse>> ListMyLopAsync(Guid giaoVienId, CancellationToken ct);

    /// <summary>Roster (chỉ Role=Student) của 1 Lớp. callerUserId/callerRole = người gọi — Admin luôn
    /// được, Teacher chỉ được nếu đúng là GiaoVienId của lopId đó (cùng pattern data-dependent với
    /// ChangeChucVuAsync bên dưới, vì không thể khai báo tĩnh bằng [Authorize(Roles=)]).</summary>
    Task<IReadOnlyList<UserResponse>> ListHocVienAsync(Guid lopId, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Việc C (2026-08-16) — bản tối thiểu (Id/Name, không Email) của ListHocVienAsync,
    /// dùng cho endpoint service-to-service (quiz-service gọi để dựng bảng xếp hạng theo Lớp).
    /// KHÔNG tự kiểm tra ownership ở đây — caller (quiz-service) đã tự xác thực callerRole/lopId
    /// khớp nhau TRƯỚC khi gọi tới (Student đúng lớp mình / Teacher đúng lớp mình phụ trách), y hệt
    /// nguyên tắc "tin caller đã kiểm" mà /quiz/stats/scores-by-users áp dụng theo chiều ngược lại.
    /// Chỉ còn 404 nếu lopId không tồn tại.</summary>
    Task<IReadOnlyList<HocVienIdResponse>> ListHocVienIdsAsync(Guid lopId, CancellationToken ct);

    /// <summary>Gap 2 mục 2 — callerUserId/callerRole = người gọi. Admin luôn được. Teacher: nếu
    /// lopId (mới) khác null, phải đúng là GiaoVienId của lopId ĐÍCH; nếu lopId = null (gỡ khỏi
    /// lớp), phải đúng là GiaoVienId của lớp HIỆN TẠI (trước khi gỡ) của user đó. Ghi
    /// LopActivityLog (StudentAdded/StudentRemoved) khi LopId thực sự đổi.</summary>
    Task<UserResponse> AssignLopAsync(Guid userId, Guid? lopId, Guid callerUserId, string callerRole, CancellationToken ct);

    Task<LopResponse> AssignGiaoVienAsync(Guid lopId, Guid? giaoVienId, CancellationToken ct);

    /// <summary>callerUserId/callerRole = người gọi API (không phải người bị đổi chức vụ) — cần để
    /// tự kiểm tra quyền "Admin hoặc đúng GV chủ nhiệm lớp của học viên đó" ở tầng service, vì việc
    /// này phụ thuộc dữ liệu (Lop.GiaoVienId) chứ không thể khai báo tĩnh bằng [Authorize(Roles=)].</summary>
    Task<UserResponse> ChangeChucVuAsync(Guid callerUserId, string callerRole, Guid targetUserId, string chucVu, CancellationToken ct);

    /// <summary>Việc V (2026-08-20) — sửa Cấp bậc trực tiếp trong roster hợp nhất Admin (quyết định
    /// đã duyệt: Cấp bậc SỬA ĐƯỢC, Điểm chỉ xem). Cùng pattern ownership check với ChangeChucVuAsync.</summary>
    Task<UserResponse> ChangeCapBacAsync(Guid callerUserId, string callerRole, Guid targetUserId, string? capBac, CancellationToken ct);

    /// <summary>Rà soát Lần VI (2026-08-21) — Môn học phụ trách của Giảng viên, CHỈ Admin sửa được
    /// (không có khái niệm ownership theo Lớp áp dụng ở đây).</summary>
    Task<UserResponse> ChangeMonHocPhuTrachAsync(string callerRole, Guid targetUserId, string? monHoc, CancellationToken ct);

    /// <summary>Rà soát Lần XIV (2026-08-21) — Chức vụ chuyên môn của Giảng viên, Admin sửa được cho
    /// người khác (đường tự sửa của GV qua UpdateProfileAsync vẫn giữ nguyên, cùng field). Không có
    /// khái niệm ownership theo Lớp, cùng pattern ChangeMonHocPhuTrachAsync.</summary>
    Task<UserResponse> ChangeChucVuGvAsync(string callerRole, Guid targetUserId, string? chucVuGV, CancellationToken ct);

    /// <summary>Rà soát Lần XVIII (2026-08-22) — Admin sửa Họ tên + Năm học của bất kỳ user nào,
    /// Admin-only, không phân biệt role của target (khác UpdateProfileAsync — self-service, mỗi
    /// field chỉ áp dụng đúng role của CHÍNH người gọi).</summary>
    Task<UserResponse> AdminEditUserAsync(string callerRole, Guid targetUserId, string name, string? namHoc, CancellationToken ct);

    /// <summary>Gap 2 mục 3 — nhật ký hoạt động của 1 Lớp, mới nhất trước. Admin luôn được, Teacher
    /// chỉ được nếu đúng là GiaoVienId của lopId đó (cùng pattern ownership check ở trên).</summary>
    Task<IReadOnlyList<LopActivityLogResponse>> GetLopActivityLogAsync(Guid lopId, Guid callerUserId, string callerRole, int top, CancellationToken ct);

    /// <summary>Việc 4.2 mục 3 (2026-08-19) — bản KHÔNG giới hạn top, KHÔNG kiểm tra ownership, chỉ
    /// dùng nội bộ bởi admin-service (RequireInternalServiceKeyFilter ở endpoint) để dựng file
    /// backup ĐẦY ĐỦ trước khi xóa toàn bộ dữ liệu Lớp — khác GetLopActivityLogAsync ở trên (dùng
    /// cho UI xem nhật ký, có cap 200 dòng và ownership check).</summary>
    Task<IReadOnlyList<LopActivityLogResponse>> GetAllLopActivityLogInternalAsync(Guid lopId, CancellationToken ct);

    /// <summary>Việc 4.2 mục 3 — XÓA VĨNH VIỄN toàn bộ tài khoản học viên (Role=Student, LopId=id)
    /// + nhật ký hoạt động của Lớp + chính Lớp. KHÔNG xóa tài khoản GV chủ nhiệm (chỉ gỡ liên kết,
    /// tự động xảy ra khi Lop bị xóa), KHÔNG đụng Khóa. Đây là bước CUỐI trong saga cross-service
    /// (quiz-service → progress-service → auth-service) — gọi khi backup đã tải về máy Admin và
    /// Admin đã xác nhận nhiều lớp an toàn (xem AdminService.Api's LopDeletionService). Idempotent:
    /// nếu Lớp đã bị xóa từ 1 lần gọi trước, trả về thành công không lỗi (xem DeleteAllLopDataResponse).</summary>
    Task<DeleteAllLopDataResponse> DeleteAllLopDataAsync(Guid lopId, CancellationToken ct);
}
