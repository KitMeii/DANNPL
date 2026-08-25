using AuthService.Api.Data;
using AuthService.Api.Dtos;
using AuthService.Api.Entities;
using AuthService.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;

namespace AuthService.Api.Services;

public sealed class AuthServiceImpl(AuthDbContext db, IJwtTokenService tokenService, IAvatarStorage avatarStorage) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (emailTaken)
        {
            throw new ConflictException("Email đã được đăng ký.");
        }

        // Self-registration is always Student — Teacher/Admin accounts are only created by
        // admin-service, closing the F1 privilege-escalation hole from the old Supabase RLS design.
        // CapBac/SoDienThoai/NamHoc (Việc 3.1) tùy chọn, chỉ có ý nghĩa cho Student nên nhận thẳng
        // ở đây — validator đã đảm bảo giá trị hợp lệ nếu có (không cần strip theo role như
        // UpdateProfileAsync, vì đăng ký luôn là Student).
        var user = new User
        {
            Email = normalizedEmail,
            Name = request.Name.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = Roles.Student,
            CapBac = NormalizeOptional(request.CapBac),
            SoDienThoai = NormalizeOptional(request.SoDienThoai),
            NamHoc = NormalizeOptional(request.NamHoc),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationFailedException("Email hoặc mật khẩu không đúng.");
        }

        if (user.IsLocked)
        {
            throw new AuthenticationFailedException("Tài khoản đã bị khóa. Liên hệ Quản trị viên để được hỗ trợ.");
        }

        return BuildAuthResponse(user);
    }

    public async Task<UserResponse> GetByIdAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        return ToUserResponse(user);
    }

    public async Task<IReadOnlyList<UserNameResponse>> GetNamesByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        return await db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserNameResponse(u.Id, u.Name, u.Role))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserResponse>> ListUsersAsync(string? role, Guid? lopId, Guid? khoaId, CancellationToken ct)
    {
        var query = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (lopId is not null)
        {
            query = query.Where(u => u.LopId == lopId);
        }

        if (khoaId is not null)
        {
            // Vật chất hóa danh sách LopId thành List<Guid> thật trước khi Contains() — nested
            // IQueryable-trong-IQueryable không dịch được nhất quán trên EF Core InMemory provider
            // (dùng cho test local), dù chạy đúng trên SQL Server thật.
            var lopIdsInKhoa = await db.Lops.Where(l => l.KhoaId == khoaId).Select(l => l.Id).ToListAsync(ct);
            query = query.Where(u => u.LopId != null && lopIdsInKhoa.Contains(u.LopId!.Value));
        }

        return await query.OrderBy(u => u.Name)
            .Select(u => new UserResponse(
                u.Id, u.Email, u.Name, u.Role, u.LopId, u.ChucVu, u.AvatarUrl,
                u.CapBac, u.SoDienThoai, u.NamHoc, u.BoMonKhoa,
                u.ChucVuGV, u.MonHocPhuTrach, u.IsLocked))
            .ToListAsync(ct);
    }

    public async Task<UserResponse> ChangeRoleAsync(Guid userId, string newRole, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        user.Role = newRole;
        await db.SaveChangesAsync(ct);

        return ToUserResponse(user);
    }

    public async Task<IReadOnlyList<StudentSearchResponse>> SearchStudentsByEmailAsync(string email, CancellationToken ct)
    {
        var trimmed = email.Trim();
        if (trimmed.Length < 3)
        {
            return [];
        }

        return await db.Users
            .Where(u => u.Role == Roles.Student && EF.Functions.Like(u.Email, $"%{trimmed}%"))
            .OrderBy(u => u.Email)
            .Take(10)
            .Select(u => new StudentSearchResponse(u.Id, u.Name, u.Email, u.LopId))
            .ToListAsync(ct);
    }

    public async Task<UserResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        user.Name = request.Name.Trim();

        // Việc 3.1 — mỗi field chỉ áp dụng đúng role của nó, kể cả khi client gửi lên field không
        // thuộc role mình (VD Teacher gửi CapBac) — bỏ qua thay vì lỗi, đơn giản hơn cho frontend
        // (không cần lọc field trước khi gửi) mà vẫn giữ dữ liệu sạch đúng ngữ nghĩa theo role.
        if (user.Role == Roles.Student)
        {
            user.CapBac = NormalizeOptional(request.CapBac);
            user.SoDienThoai = NormalizeOptional(request.SoDienThoai);
            user.NamHoc = NormalizeOptional(request.NamHoc);
        }
        else if (user.Role == Roles.Teacher)
        {
            // Rà soát Lần VI (2026-08-21) — GV cũng cần Cấp bậc (dùng chung CapBacValues với Student,
            // học viện quân sự nên GV cũng mang quân hàm) + ChucVuGV (chức danh học thuật, enum riêng
            // ChucVuGvValues) — cả 2 GV tự sửa được, KHÁC MonHocPhuTrach (chỉ Admin sửa, xem
            // KhoaLopService.ChangeMonHocPhuTrachAsync).
            user.CapBac = NormalizeOptional(request.CapBac);
            user.SoDienThoai = NormalizeOptional(request.SoDienThoai);
            user.BoMonKhoa = NormalizeOptional(request.BoMonKhoa);
            user.ChucVuGV = NormalizeOptional(request.ChucVuGV);
        }
        else if (user.Role == Roles.Admin)
        {
            // Rà soát Lần XVI (2026-08-21) — Admin trước đây hoàn toàn không tự sửa được gì ngoài
            // Họ tên (nhánh else-if không có case Admin, request bị bỏ qua im lặng — đúng thiết kế
            // ban đầu nhưng người dùng yêu cầu Admin cũng cần đủ SĐT/Cấp bậc/Chức vụ như GV). Dùng
            // lại NGUYÊN 3 field CapBac/SoDienThoai/ChucVuGV (không thêm cột mới) — danh sách
            // ChucVuGvValues đã có sẵn "Trưởng bộ môn/Trưởng khoa" hợp lý cho Admin kiêm quản lý học
            // thuật. KHÔNG có BoMonKhoa/MonHocPhuTrach (không áp dụng cho Admin theo đúng yêu cầu).
            user.CapBac = NormalizeOptional(request.CapBac);
            user.SoDienThoai = NormalizeOptional(request.SoDienThoai);
            user.ChucVuGV = NormalizeOptional(request.ChucVuGV);
        }

        await db.SaveChangesAsync(ct);

        return ToUserResponse(user);
    }

    /// <summary>Rà soát Lần VI (2026-08-21) — đổi mật khẩu self-service, mọi role. Xác thực mật khẩu
    /// hiện tại bằng đúng PasswordHasher.Verify dùng ở LoginAsync trước khi cho phép đổi.</summary>
    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
        {
            // UnauthorizedAccessException (403), KHÔNG dùng AuthenticationFailedException (401) —
            // 401 từ apiFetch() (api-client.js) kích hoạt xử lý "phiên hết hạn" TOÀN CỤC (tự
            // clearSession() + điều hướng /auth.html), sẽ đăng xuất nhầm người dùng ĐANG đăng nhập
            // hợp lệ chỉ vì gõ sai mật khẩu HIỆN TẠI lúc đổi mật khẩu — lỗi thật phát hiện qua
            // Playwright khi test luồng đổi mật khẩu (Rà soát Lần VI, 2026-08-21).
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");
        }

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        await db.SaveChangesAsync(ct);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<UserResponse> CreateUserByAdminAsync(CreateUserByAdminRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (emailTaken)
        {
            throw new ConflictException("Email đã được đăng ký.");
        }

        var user = new User
        {
            Email = normalizedEmail,
            Name = request.Name.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = request.Role,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return ToUserResponse(user);
    }

    public async Task<UserResponse> SetUserLockedAsync(Guid targetUserId, bool isLocked, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([targetUserId], ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        user.IsLocked = isLocked;
        await db.SaveChangesAsync(ct);

        return ToUserResponse(user);
    }

    public async Task<UserResponse> UpdateAvatarAsync(Guid userId, Stream fileContent, string fileName, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("Không tìm thấy người dùng.");

        var uploaded = await avatarStorage.UploadAsync(fileContent, fileName, ct);

        var oldPublicId = user.AvatarPublicId;
        user.AvatarUrl = uploaded.Url;
        user.AvatarPublicId = uploaded.PublicId;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(oldPublicId))
        {
            try
            {
                await avatarStorage.DeleteAsync(oldPublicId, ct);
            }
            catch
            {
                // Best-effort — ảnh cũ mồ côi trên Cloudinary là vấn đề dọn dẹp nhỏ, không phải lỗi
                // chặn người dùng đổi avatar mới. Cùng convention với MaterialService.DeleteAsync.
            }
        }

        return ToUserResponse(user);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var token = tokenService.IssueAccessToken(user.Id.ToString(), user.Email, user.Name, user.Role);
        return new AuthResponse(token.AccessToken, token.ExpiresAtUtc, ToUserResponse(user));
    }

    private static UserResponse ToUserResponse(User user) => new(
        user.Id, user.Email, user.Name, user.Role, user.LopId, user.ChucVu, user.AvatarUrl,
        user.CapBac, user.SoDienThoai, user.NamHoc, user.BoMonKhoa,
        user.ChucVuGV, user.MonHocPhuTrach, user.IsLocked);
}
