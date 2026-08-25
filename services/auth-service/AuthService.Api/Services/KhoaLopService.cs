using AuthService.Api.Data;
using AuthService.Api.Dtos;
using AuthService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using Shared.Infrastructure.Common;

namespace AuthService.Api.Services;

public sealed class KhoaLopService(AuthDbContext db) : IKhoaLopService
{
    public async Task<KhoaResponse> CreateKhoaAsync(CreateKhoaRequest request, CancellationToken ct)
    {
        var khoa = new Khoa { Ten = request.Ten.Trim() };
        db.Khoas.Add(khoa);
        await db.SaveChangesAsync(ct);
        return ToKhoaResponse(khoa);
    }

    public async Task<IReadOnlyList<KhoaResponse>> ListKhoaAsync(CancellationToken ct) =>
        await db.Khoas.OrderBy(k => k.Ten)
            .Select(k => new KhoaResponse(k.Id, k.Ten))
            .ToListAsync(ct);

    public async Task<KhoaResponse> GetKhoaByIdAsync(Guid id, CancellationToken ct)
    {
        var khoa = await db.Khoas.FindAsync([id], ct) ?? throw new NotFoundException("Không tìm thấy khóa.");
        return ToKhoaResponse(khoa);
    }

    public async Task<KhoaResponse> UpdateKhoaAsync(Guid id, UpdateKhoaRequest request, CancellationToken ct)
    {
        var khoa = await db.Khoas.FindAsync([id], ct) ?? throw new NotFoundException("Không tìm thấy khóa.");
        khoa.Ten = request.Ten.Trim();
        await db.SaveChangesAsync(ct);
        return ToKhoaResponse(khoa);
    }

    public async Task DeleteKhoaAsync(Guid id, CancellationToken ct)
    {
        var khoa = await db.Khoas.FindAsync([id], ct) ?? throw new NotFoundException("Không tìm thấy khóa.");

        var hasLop = await db.Lops.AnyAsync(l => l.KhoaId == id, ct);
        if (hasLop)
        {
            throw new ConflictException("Không thể xóa Khóa đang có Lớp — xóa hoặc chuyển các Lớp trước.");
        }

        db.Khoas.Remove(khoa);
        await db.SaveChangesAsync(ct);
    }

    public async Task<LopResponse> CreateLopAsync(CreateLopRequest request, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var khoaExists = await db.Khoas.AnyAsync(k => k.Id == request.KhoaId, ct);
        if (!khoaExists)
        {
            throw new NotFoundException("Không tìm thấy khóa.");
        }

        var lop = new Lop
        {
            Ten = request.Ten.Trim(),
            KhoaId = request.KhoaId,
            // Việc 4.2 — Teacher tạo lớp tự động thành GV chủ nhiệm; Admin tạo thì để trống như cũ.
            GiaoVienId = callerRole == Roles.Teacher ? callerUserId : null,
            NamHoc = string.IsNullOrWhiteSpace(request.NamHoc) ? null : request.NamHoc.Trim(),
        };
        db.Lops.Add(lop);
        await db.SaveChangesAsync(ct);
        return ToLopResponse(lop);
    }

    public async Task<IReadOnlyList<LopResponse>> ListLopAsync(Guid? khoaId, CancellationToken ct)
    {
        var query = db.Lops.AsQueryable();
        if (khoaId is not null)
        {
            query = query.Where(l => l.KhoaId == khoaId);
        }

        return await query.OrderBy(l => l.Ten)
            .Select(l => new LopResponse(l.Id, l.Ten, l.KhoaId, l.GiaoVienId, l.NamHoc))
            .ToListAsync(ct);
    }

    public async Task<LopResponse> GetLopByIdAsync(Guid id, CancellationToken ct)
    {
        var lop = await db.Lops.FindAsync([id], ct) ?? throw new NotFoundException("Không tìm thấy lớp.");
        return ToLopResponse(lop);
    }

    public async Task<LopResponse> UpdateLopAsync(Guid id, UpdateLopRequest request, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var lop = await db.Lops.FindAsync([id], ct) ?? throw new NotFoundException("Không tìm thấy lớp.");

        if (callerRole != Roles.Admin && lop.GiaoVienId != callerUserId)
        {
            throw new UnauthorizedAccessException("Chỉ Admin hoặc giáo viên chủ nhiệm của lớp này mới được đổi tên.");
        }

        lop.Ten = request.Ten.Trim();
        // request.NamHoc == null nghĩa là "không đổi" (Teacher's saveRenameLop() chỉ gửi Ten, không
        // biết/không cần đụng Năm học) — CHỈ ghi đè khi caller thật sự gửi 1 chuỗi (kể cả rỗng, để
        // vẫn có đường xóa Năm học nếu cần: gửi "" -> lưu null). Tránh Teacher đổi tên vô tình xóa
        // mất Năm học Admin đã nhập trước đó.
        if (request.NamHoc is not null)
        {
            lop.NamHoc = string.IsNullOrWhiteSpace(request.NamHoc) ? null : request.NamHoc.Trim();
        }

        // Rà soát Lần IX (2026-08-21) — cho phép đổi Khóa của Lớp từ modal "Sửa lớp" (trước đây chỉ
        // chọn được Khóa lúc tạo, không có đường sửa lại). request.KhoaId == null nghĩa là "không
        // đổi", cùng quy ước NamHoc ở trên.
        if (request.KhoaId is { } khoaId && khoaId != lop.KhoaId)
        {
            var khoaExists = await db.Khoas.AnyAsync(k => k.Id == khoaId, ct);
            if (!khoaExists)
            {
                throw new NotFoundException("Không tìm thấy khóa.");
            }

            lop.KhoaId = khoaId;
        }

        await db.SaveChangesAsync(ct);
        return ToLopResponse(lop);
    }

    public async Task DeleteLopAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var lop = await db.Lops.FindAsync([id], ct) ?? throw new NotFoundException("Không tìm thấy lớp.");

        if (callerRole != Roles.Admin && lop.GiaoVienId != callerUserId)
        {
            throw new UnauthorizedAccessException("Chỉ Admin hoặc giáo viên chủ nhiệm của lớp này mới được xóa.");
        }

        var hasStudents = await db.Users.AnyAsync(u => u.LopId == id, ct);
        if (hasStudents)
        {
            throw new ConflictException("Không thể xóa Lớp đang có học viên — chuyển học viên sang lớp khác trước.");
        }

        db.Lops.Remove(lop);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LopResponse>> ListMyLopAsync(Guid giaoVienId, CancellationToken ct) =>
        await db.Lops.Where(l => l.GiaoVienId == giaoVienId)
            .OrderBy(l => l.Ten)
            .Select(l => new LopResponse(l.Id, l.Ten, l.KhoaId, l.GiaoVienId, l.NamHoc))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserResponse>> ListHocVienAsync(Guid lopId, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var lop = await db.Lops.FindAsync([lopId], ct) ?? throw new NotFoundException("Không tìm thấy lớp.");

        if (callerRole != Roles.Admin && lop.GiaoVienId != callerUserId)
        {
            throw new UnauthorizedAccessException("Chỉ Admin hoặc giáo viên chủ nhiệm của lớp này mới được xem danh sách học viên.");
        }

        return await db.Users.Where(u => u.LopId == lopId && u.Role == Roles.Student)
            .OrderBy(u => u.Name)
            .Select(u => new UserResponse(
                u.Id, u.Email, u.Name, u.Role, u.LopId, u.ChucVu, u.AvatarUrl,
                u.CapBac, u.SoDienThoai, u.NamHoc, u.BoMonKhoa,
                u.ChucVuGV, u.MonHocPhuTrach, u.IsLocked))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<HocVienIdResponse>> ListHocVienIdsAsync(Guid lopId, CancellationToken ct)
    {
        _ = await db.Lops.FindAsync([lopId], ct) ?? throw new NotFoundException("Không tìm thấy lớp.");

        return await db.Users.Where(u => u.LopId == lopId && u.Role == Roles.Student)
            .OrderBy(u => u.Name)
            .Select(u => new HocVienIdResponse(u.Id, u.Name, u.ChucVu, u.CapBac, u.AvatarUrl))
            .ToListAsync(ct);
    }

    public async Task<UserResponse> AssignLopAsync(Guid userId, Guid? lopId, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct) ?? throw new NotFoundException("Không tìm thấy người dùng.");
        var oldLopId = user.LopId;

        Lop? targetLop = null;
        if (lopId is not null)
        {
            targetLop = await db.Lops.FindAsync([lopId], ct) ?? throw new NotFoundException("Không tìm thấy lớp.");

            if (callerRole != Roles.Admin && targetLop.GiaoVienId != callerUserId)
            {
                throw new UnauthorizedAccessException("Chỉ Admin hoặc giáo viên chủ nhiệm của lớp đích mới được gán học viên vào lớp này.");
            }
        }
        else if (callerRole != Roles.Admin)
        {
            // Gỡ khỏi lớp — kiểm tra lớp HIỆN TẠI (trước khi gỡ), không phải lớp mới (null).
            var currentLop = oldLopId is null ? null : await db.Lops.FindAsync([oldLopId], ct);
            if (currentLop is null || currentLop.GiaoVienId != callerUserId)
            {
                throw new UnauthorizedAccessException("Chỉ Admin hoặc giáo viên chủ nhiệm của lớp hiện tại mới được gỡ học viên khỏi lớp.");
            }
        }

        if (oldLopId != lopId)
        {
            if (lopId is not null)
            {
                db.LopActivityLogs.Add(new LopActivityLog
                {
                    LopId = lopId.Value,
                    ActorUserId = callerUserId,
                    ActionType = LopActivityActionTypes.StudentAdded,
                    TargetUserId = userId,
                    OldValue = oldLopId?.ToString(),
                });
            }
            else if (oldLopId is not null)
            {
                db.LopActivityLogs.Add(new LopActivityLog
                {
                    LopId = oldLopId.Value,
                    ActorUserId = callerUserId,
                    ActionType = LopActivityActionTypes.StudentRemoved,
                    TargetUserId = userId,
                });
            }
        }

        user.LopId = lopId;
        await db.SaveChangesAsync(ct);
        return ToUserResponse(user);
    }

    public async Task<LopResponse> AssignGiaoVienAsync(Guid lopId, Guid? giaoVienId, CancellationToken ct)
    {
        var lop = await db.Lops.FindAsync([lopId], ct) ?? throw new NotFoundException("Không tìm thấy lớp.");

        if (giaoVienId is not null)
        {
            var teacherExists = await db.Users.AnyAsync(u => u.Id == giaoVienId && u.Role == Roles.Teacher, ct);
            if (!teacherExists)
            {
                throw new NotFoundException("Không tìm thấy giáo viên.");
            }
        }

        lop.GiaoVienId = giaoVienId;
        await db.SaveChangesAsync(ct);
        return ToLopResponse(lop);
    }

    public async Task<UserResponse> ChangeChucVuAsync(Guid callerUserId, string callerRole, Guid targetUserId, string chucVu, CancellationToken ct)
    {
        var target = await db.Users.FindAsync([targetUserId], ct) ?? throw new NotFoundException("Không tìm thấy học viên.");

        if (callerRole != Roles.Admin)
        {
            var isChuNhiem = callerRole == Roles.Teacher
                && target.LopId is not null
                && await db.Lops.AnyAsync(l => l.Id == target.LopId && l.GiaoVienId == callerUserId, ct);

            if (!isChuNhiem)
            {
                throw new UnauthorizedAccessException("Chỉ Admin hoặc giáo viên chủ nhiệm của lớp này mới được sửa chức vụ.");
            }
        }

        // Chỉ ghi log nếu target đang thuộc 1 Lớp (nhật ký này gắn theo LopId) và giá trị thực sự
        // đổi — tránh rác log khi FE gọi lại cùng giá trị.
        if (target.LopId is not null && target.ChucVu != chucVu)
        {
            db.LopActivityLogs.Add(new LopActivityLog
            {
                LopId = target.LopId.Value,
                ActorUserId = callerUserId,
                ActionType = LopActivityActionTypes.ChucVuChanged,
                TargetUserId = targetUserId,
                OldValue = target.ChucVu,
                NewValue = chucVu,
            });
        }

        target.ChucVu = chucVu;
        await db.SaveChangesAsync(ct);
        return ToUserResponse(target);
    }

    // Việc V (2026-08-20, rà soát Lần II mục 1.6/1.9/1.11) — Admin (hoặc GV chủ nhiệm của target)
    // sửa Cấp bậc trực tiếp trong roster hợp nhất. Cùng pattern ownership check với ChangeChucVuAsync
    // ở trên — không ghi LopActivityLog (bảng đó chỉ theo dõi 3 loại hành động đã định nghĩa sẵn,
    // Cấp bậc không phải hành động "thuộc về Lớp" theo đúng nghĩa, mà là hồ sơ cá nhân học viên).
    public async Task<UserResponse> ChangeCapBacAsync(Guid callerUserId, string callerRole, Guid targetUserId, string? capBac, CancellationToken ct)
    {
        var target = await db.Users.FindAsync([targetUserId], ct) ?? throw new NotFoundException("Không tìm thấy học viên.");

        if (callerRole != Roles.Admin)
        {
            var isChuNhiem = callerRole == Roles.Teacher
                && target.LopId is not null
                && await db.Lops.AnyAsync(l => l.Id == target.LopId && l.GiaoVienId == callerUserId, ct);

            if (!isChuNhiem)
            {
                throw new UnauthorizedAccessException("Chỉ Admin hoặc giáo viên chủ nhiệm của lớp này mới được sửa cấp bậc.");
            }
        }

        target.CapBac = string.IsNullOrWhiteSpace(capBac) ? null : capBac;
        await db.SaveChangesAsync(ct);
        return ToUserResponse(target);
    }

    public async Task<IReadOnlyList<LopActivityLogResponse>> GetLopActivityLogAsync(Guid lopId, Guid callerUserId, string callerRole, int top, CancellationToken ct)
    {
        var lop = await db.Lops.FindAsync([lopId], ct) ?? throw new NotFoundException("Không tìm thấy lớp.");

        if (callerRole != Roles.Admin && lop.GiaoVienId != callerUserId)
        {
            throw new UnauthorizedAccessException("Chỉ Admin hoặc giáo viên chủ nhiệm của lớp này mới được xem nhật ký hoạt động.");
        }

        var logs = await db.LopActivityLogs
            .Where(l => l.LopId == lopId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(top)
            .ToListAsync(ct);

        var userIds = logs.Select(l => l.ActorUserId).Concat(logs.Select(l => l.TargetUserId)).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return logs
            .Select(l => new LopActivityLogResponse(
                l.Id,
                l.LopId,
                l.ActorUserId,
                names.GetValueOrDefault(l.ActorUserId, "?"),
                l.ActionType,
                l.TargetUserId,
                names.GetValueOrDefault(l.TargetUserId, "?"),
                l.OldValue,
                l.NewValue,
                l.CreatedAtUtc))
            .ToList();
    }

    public async Task<IReadOnlyList<LopActivityLogResponse>> GetAllLopActivityLogInternalAsync(Guid lopId, CancellationToken ct)
    {
        var logs = await db.LopActivityLogs
            .Where(l => l.LopId == lopId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);

        var userIds = logs.Select(l => l.ActorUserId).Concat(logs.Select(l => l.TargetUserId)).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return logs
            .Select(l => new LopActivityLogResponse(
                l.Id,
                l.LopId,
                l.ActorUserId,
                names.GetValueOrDefault(l.ActorUserId, "?"),
                l.ActionType,
                l.TargetUserId,
                names.GetValueOrDefault(l.TargetUserId, "?"),
                l.OldValue,
                l.NewValue,
                l.CreatedAtUtc))
            .ToList();
    }

    public async Task<DeleteAllLopDataResponse> DeleteAllLopDataAsync(Guid lopId, CancellationToken ct)
    {
        var lop = await db.Lops.FindAsync([lopId], ct);
        if (lop is null)
        {
            // Idempotent no-op — xem remarks ở DeleteAllLopDataResponse.
            return new DeleteAllLopDataResponse(0, 0, false);
        }

        // Thứ tự: dữ liệu phụ thuộc (nhật ký) trước, tài khoản học viên sau, chính Lớp cuối cùng —
        // CHỈ Role=Student (KHÔNG đụng tài khoản GV chủ nhiệm dù LopId trùng không xảy ra vì GV
        // không có LopId, chỉ có GiaoVienId ở bảng Lop trỏ ngược).
        var activityLogsDeleted = await db.LopActivityLogs.Where(l => l.LopId == lopId).ExecuteDeleteAsync(ct);
        var usersDeleted = await db.Users.Where(u => u.LopId == lopId && u.Role == Roles.Student).ExecuteDeleteAsync(ct);

        db.Lops.Remove(lop);
        await db.SaveChangesAsync(ct);

        return new DeleteAllLopDataResponse(usersDeleted, activityLogsDeleted, true);
    }

    private static KhoaResponse ToKhoaResponse(Khoa khoa) => new(khoa.Id, khoa.Ten);

    private static LopResponse ToLopResponse(Lop lop) => new(lop.Id, lop.Ten, lop.KhoaId, lop.GiaoVienId, lop.NamHoc);

    // Việc V (2026-08-20) — thêm CapBac/SoDienThoai/NamHoc/BoMonKhoa vào projection (trước chỉ
    // Id/Email/Name/Role/LopId/ChucVu) — ChangeCapBacAsync ở trên cần trả về đúng CapBac vừa sửa,
    // không phải luôn null như trước.
    private static UserResponse ToUserResponse(User user) =>
        new(user.Id, user.Email, user.Name, user.Role, user.LopId, user.ChucVu, user.AvatarUrl,
            user.CapBac, user.SoDienThoai, user.NamHoc, user.BoMonKhoa,
            user.ChucVuGV, user.MonHocPhuTrach, user.IsLocked);

    /// <summary>Rà soát Lần VI (2026-08-21) — Môn học phụ trách CHỈ Admin sửa được (không có khái
    /// niệm "GV chủ nhiệm" áp dụng ở đây như ChangeCapBac/ChangeChucVu — đây là phân công môn dạy
    /// cho chính GV đó, không gắn với 1 Lớp cụ thể nào).</summary>
    public async Task<UserResponse> ChangeMonHocPhuTrachAsync(string callerRole, Guid targetUserId, string? monHoc, CancellationToken ct)
    {
        if (callerRole != Roles.Admin)
        {
            throw new UnauthorizedAccessException("Chỉ Admin mới được phân công môn học phụ trách.");
        }

        var target = await db.Users.FindAsync([targetUserId], ct) ?? throw new NotFoundException("Không tìm thấy giảng viên.");
        target.MonHocPhuTrach = string.IsNullOrWhiteSpace(monHoc) ? null : monHoc.Trim();
        await db.SaveChangesAsync(ct);
        return ToUserResponse(target);
    }

    /// <summary>Rà soát Lần XIV (2026-08-21) — Admin sửa Chức vụ chuyên môn của GV trực tiếp (panel
    /// "Quản lý GV"), cùng pattern Admin-only với ChangeMonHocPhuTrachAsync ở trên.</summary>
    public async Task<UserResponse> ChangeChucVuGvAsync(string callerRole, Guid targetUserId, string? chucVuGV, CancellationToken ct)
    {
        if (callerRole != Roles.Admin)
        {
            throw new UnauthorizedAccessException("Chỉ Admin mới được sửa chức vụ của giảng viên khác.");
        }

        var target = await db.Users.FindAsync([targetUserId], ct) ?? throw new NotFoundException("Không tìm thấy giảng viên.");
        target.ChucVuGV = string.IsNullOrWhiteSpace(chucVuGV) ? null : chucVuGV.Trim();
        await db.SaveChangesAsync(ct);
        return ToUserResponse(target);
    }

    /// <summary>Rà soát Lần XVIII (2026-08-22) — Admin sửa Họ tên + Năm học trực tiếp cho bất kỳ
    /// user nào (panel Quản lý GV/Quản lý Tài khoản) — trước đây Name CHỈ tự sửa qua UpdateProfileAsync.</summary>
    public async Task<UserResponse> AdminEditUserAsync(string callerRole, Guid targetUserId, string name, string? namHoc, CancellationToken ct)
    {
        if (callerRole != Roles.Admin)
        {
            throw new UnauthorizedAccessException("Chỉ Admin mới được sửa thông tin tài khoản khác.");
        }

        var target = await db.Users.FindAsync([targetUserId], ct) ?? throw new NotFoundException("Không tìm thấy người dùng.");
        target.Name = name.Trim();
        if (namHoc is not null)
        {
            target.NamHoc = string.IsNullOrWhiteSpace(namHoc) ? null : namHoc.Trim();
        }
        await db.SaveChangesAsync(ct);
        return ToUserResponse(target);
    }
}
