using AuthService.Api.Dtos;

namespace AuthService.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<UserResponse> GetByIdAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<UserNameResponse>> GetNamesByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    /// <summary>lopId/khoaId lọc thêm theo Lớp/Khóa (Bước C) — kết hợp AND với role nếu cả 2 cùng
    /// truyền. khoaId join qua bảng Lop (User không có KhoaId trực tiếp).</summary>
    Task<IReadOnlyList<UserResponse>> ListUsersAsync(string? role, Guid? lopId, Guid? khoaId, CancellationToken ct);
    Task<UserResponse> ChangeRoleAsync(Guid userId, string newRole, CancellationToken ct);
    Task<UserResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct);

    /// <summary>Gap 2 mục 2 (Hướng B) — tìm học viên theo email để GV chủ nhiệm gán vào lớp mình.
    /// Chỉ Role=Student (không cho dò email Admin/Teacher khác), yêu cầu tối thiểu 3 ký tự để
    /// tránh quét rộng, giới hạn 10 kết quả — xem StudentSearchResponse remarks về nguyên tắc tối
    /// thiểu thông tin trả về.</summary>
    Task<IReadOnlyList<StudentSearchResponse>> SearchStudentsByEmailAsync(string email, CancellationToken ct);

    /// <summary>userId luôn lấy từ JWT của người gọi (endpoint không nhận id khác từ client) — mọi
    /// user chỉ đổi được avatar của chính mình, cùng pattern với ListMyLopAsync. Xóa avatar cũ trên
    /// Cloudinary (best-effort) nếu có, trước khi lưu avatar mới.</summary>
    Task<UserResponse> UpdateAvatarAsync(Guid userId, Stream fileContent, string fileName, CancellationToken ct);

    /// <summary>Rà soát Lần VI (2026-08-21) — đổi mật khẩu self-service, mọi role. userId lấy từ
    /// JWT (không nhận id khác) — chỉ đổi được mật khẩu của chính mình.</summary>
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);

    /// <summary>Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp với Role bất kỳ, không
    /// qua luồng tự đăng ký (RegisterAsync luôn tạo Student).</summary>
    Task<UserResponse> CreateUserByAdminAsync(CreateUserByAdminRequest request, CancellationToken ct);

    /// <summary>Rà soát Lần XII (2026-08-21) — Admin khóa/mở khóa tài khoản (chặn đăng nhập, giữ
    /// nguyên dữ liệu). Chặn tự khóa chính mình ở endpoint (cùng vị trí/pattern với ChangeRoleAsync
    /// — xem AuthEndpoints.cs), không phải ở đây.</summary>
    Task<UserResponse> SetUserLockedAsync(Guid targetUserId, bool isLocked, CancellationToken ct);
}
