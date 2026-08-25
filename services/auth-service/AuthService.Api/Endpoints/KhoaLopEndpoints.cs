using System.Security.Claims;
using AuthService.Api.Dtos;
using AuthService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace AuthService.Api.Endpoints;

/// <summary>
/// Endpoint CRUD Khóa/Lớp + gán học viên/giáo viên/chức vụ. Gọi trực tiếp từ frontend qua gateway
/// tới auth-service (không qua admin-service như /users, /users/{id}/role — những endpoint đó có
/// RequireInternalServiceKeyFilter vì admin-service ghi audit log cho việc đổi Role; Khóa/Lớp chưa
/// có audit log tương đương, ngoài phạm vi Bước B đã thống nhất).
/// </summary>
public static class KhoaLopEndpoints
{
    public static IEndpointRouteBuilder MapKhoaLopEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("KhoaLop");

        group.MapPost("/khoa", async (CreateKhoaRequest request, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.CreateKhoaAsync(request, ct);
                return Results.Created($"/api/v1/auth/khoa/{result.Id}", ApiResponse<KhoaResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateKhoaRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Việc 4.2 (2026-08-19) — mở cho Teacher (trước Admin-only): cần để chọn Khóa khi tạo Lớp
        // mới (Teacher không tạo được Khóa, chỉ cần ĐỌC danh sách để chọn). Đọc-only, cùng mức nhạy
        // cảm với GET /khoa/{id} (nhãn tổ chức, không phải dữ liệu nhạy cảm) — chỉ GET /khoa (liệt
        // kê hàng loạt) trước đây Admin-only vì chưa có nhu cầu, không phải vì rủi ro bảo mật.
        group.MapGet("/khoa", async (IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.ListKhoaAsync(ct);
                return Results.Ok(ApiResponse<IReadOnlyList<KhoaResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // CHỦ Ý (không phải lỗ hổng bỏ sót): bất kỳ user đã đăng nhập nào cũng đọc được — không
        // giới hạn Admin, và KHÔNG kiểm tra id truyền vào có phải Khóa/Lớp của chính người gọi hay
        // không (GetKhoaByIdAsync/GetLopByIdAsync chỉ lookup thẳng theo id). Nghĩa là 1 học viên
        // có thể xem tên của BẤT KỲ Khóa/Lớp nào, không riêng lớp mình — chấp nhận được vì Tên
        // Khóa/Lớp chỉ là nhãn tổ chức (vd "K59", "CNTT1"), không phải dữ liệu nhạy cảm, khác hẳn
        // GET /khoa và GET /lop (liệt kê hàng loạt, vẫn Admin-only). Endpoint này tồn tại để học
        // viên tự resolve tên Lớp/Khóa của MÌNH từ LopId trả về ở /me (hiển thị hồ sơ, Gap 1 fix)
        // — việc lookup được cả id của người khác chỉ là hệ quả của thiết kế đơn giản, không phải
        // yêu cầu nghiệp vụ, và không rò rỉ gì hơn ngoài 1 chuỗi tên.
        group.MapGet("/khoa/{id:guid}", async (Guid id, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.GetKhoaByIdAsync(id, ct);
                return Results.Ok(ApiResponse<KhoaResponse>.Ok(result));
            })
            .RequireAuthorization();

        group.MapPut("/khoa/{id:guid}", async (Guid id, UpdateKhoaRequest request, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.UpdateKhoaAsync(id, request, ct);
                return Results.Ok(ApiResponse<KhoaResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateKhoaRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapDelete("/khoa/{id:guid}", async (Guid id, IKhoaLopService service, CancellationToken ct) =>
            {
                await service.DeleteKhoaAsync(id, ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Việc 4.2 (2026-08-19) — mở cho Teacher (trước Admin-only): Teacher tạo lớp tự động thành
        // GV chủ nhiệm (xem KhoaLopService.CreateLopAsync). Tạo Khóa mới vẫn Admin-only, không đổi.
        group.MapPost("/lop", async (CreateLopRequest request, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.CreateLopAsync(request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Created($"/api/v1/auth/lop/{result.Id}", ApiResponse<LopResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateLopRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapGet("/lop", async (Guid? khoaId, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.ListLopAsync(khoaId, ct);
                return Results.Ok(ApiResponse<IReadOnlyList<LopResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Cùng lý do với GET /khoa/{id} ở trên — bất kỳ user đã đăng nhập nào cũng đọc được.
        group.MapGet("/lop/{id:guid}", async (Guid id, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.GetLopByIdAsync(id, ct);
                return Results.Ok(ApiResponse<LopResponse>.Ok(result));
            })
            .RequireAuthorization();

        // Gap 2: Teacher tự lấy (các) Lớp mình phụ trách — không nhận tham số giaoVienId từ client,
        // luôn suy ra từ JWT của người gọi, nên không có cách nào dò lớp của GV khác qua endpoint
        // này. Đặt trước /lop/{id:guid} trong file cho dễ đọc — thứ tự đăng ký không ảnh hưởng
        // matching vì "mine" không khớp ràng buộc :guid.
        group.MapGet("/lop/mine", async (ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.ListMyLopAsync(principal.GetUserId(), ct);
                return Results.Ok(ApiResponse<IReadOnlyList<LopResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher));

        // Gap 2: roster (chỉ Role=Student) của 1 Lớp — Admin luôn được, Teacher chỉ được nếu đúng
        // là GV chủ nhiệm của lopId đó. [RequireRole] chỉ chặn được các role khác Teacher/Admin;
        // phần "đúng lớp mình" phụ thuộc dữ liệu nên nằm ở service (ListHocVienAsync), cùng pattern
        // với ChangeChucVuAsync bên dưới.
        group.MapGet("/lop/{id:guid}/hoc-vien", async (Guid id, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.ListHocVienAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<IReadOnlyList<UserResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc C (2026-08-16): bản tối thiểu của hoc-vien ở trên (Id/Name, không Email) — chỉ
        // quiz-service gọi được (RequireInternalServiceKeyFilter), dùng cho GET /quiz/stats/
        // leaderboard-by-lop. RequireAuthorization() thường (không RequireRole) vì quiz-service
        // forward nguyên JWT gốc của người gọi (có thể là Student xem lớp mình) — biên bảo mật thật
        // là internal key, không phải role, cùng nguyên tắc /quiz/stats/scores-by-users.
        group.MapGet("/lop/{id:guid}/hoc-vien-ids", async (Guid id, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.ListHocVienIdsAsync(id, ct);
                return Results.Ok(ApiResponse<IReadOnlyList<HocVienIdResponse>>.Ok(result));
            })
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization();

        // Việc 4.2 — mở cho Teacher (trước Admin-only). [RequireRole] chỉ chặn được role khác
        // Teacher/Admin; phần "đúng lớp mình" phụ thuộc dữ liệu nên nằm ở service (ownership check),
        // cùng pattern với ChangeChucVuAsync/ListHocVienAsync.
        group.MapPut("/lop/{id:guid}", async (Guid id, UpdateLopRequest request, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.UpdateLopAsync(id, request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<LopResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateLopRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc 4.2 — mở cho Teacher (trước Admin-only). Vẫn chỉ xóa được Lớp RỖNG (guard cũ giữ
        // nguyên) — KHÔNG phải "xóa toàn bộ dữ liệu Lớp" (tính năng hủy diệt riêng, Admin-only).
        group.MapDelete("/lop/{id:guid}", async (Guid id, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                await service.DeleteLopAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPut("/lop/{id:guid}/giao-vien", async (Guid id, AssignGiaoVienRequest request, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.AssignGiaoVienAsync(id, request.GiaoVienId, ct);
                return Results.Ok(ApiResponse<LopResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<AssignGiaoVienRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Gap 2 mục 2: Admin HOẶC GV chủ nhiệm — gán vào lớp mình (LopId != null) hoặc gỡ khỏi lớp
        // mình (LopId = null) đều được, cả 2 chiều đều kiểm tra data-dependent trong
        // KhoaLopService.AssignLopAsync (không thể khai báo tĩnh bằng [Authorize(Roles=)]).
        group.MapPut("/users/{id:guid}/lop", async (Guid id, AssignLopRequest request, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.AssignLopAsync(id, request.LopId, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<AssignLopRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Admin HOẶC giáo viên chủ nhiệm đúng lớp của học viên đó — [RequireRole] chỉ chặn được
        // Student ở đây, phần kiểm tra "đúng lớp" phụ thuộc dữ liệu (Lop.GiaoVienId) nên nằm ở
        // service (KhoaLopService.ChangeChucVuAsync), không thể khai báo tĩnh.
        group.MapPut("/users/{id:guid}/chuc-vu", async (Guid id, ChangeChucVuRequest request, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.ChangeChucVuAsync(principal.GetUserId(), principal.GetRole(), id, request.ChucVu, ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<ChangeChucVuRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc V (2026-08-20, rà soát Lần II mục 1.6/1.9/1.11) — sửa Cấp bậc trực tiếp trong roster
        // hợp nhất Admin. Cùng pattern RBAC/ownership với chuc-vu ở trên.
        group.MapPut("/users/{id:guid}/cap-bac", async (Guid id, ChangeCapBacRequest request, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.ChangeCapBacAsync(principal.GetUserId(), principal.GetRole(), id, request.CapBac, ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<ChangeCapBacRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Rà soát Lần VI (2026-08-21) — Môn học phụ trách của GV, CHỈ Admin sửa được (RequireRole
        // chỉ Admin, khác cap-bac/chuc-vu ở trên cho cả Teacher chủ nhiệm).
        group.MapPut("/users/{id:guid}/mon-hoc-phu-trach", async (Guid id, ChangeMonHocPhuTrachRequest request, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.ChangeMonHocPhuTrachAsync(principal.GetRole(), id, request.MonHocPhuTrach, ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<ChangeMonHocPhuTrachRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Rà soát Lần XIV (2026-08-21) — panel "Quản lý GV": Admin sửa Chức vụ chuyên môn của GV
        // trực tiếp, cùng pattern mon-hoc-phu-trach ở trên.
        group.MapPut("/users/{id:guid}/chuc-vu-gv", async (Guid id, ChangeChucVuGvRequest request, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.ChangeChucVuGvAsync(principal.GetRole(), id, request.ChucVuGV, ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<ChangeChucVuGvRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Rà soát Lần XVIII (2026-08-22) — Admin sửa Họ tên + Năm học của bất kỳ user nào (panel
        // Quản lý GV/Quản lý Tài khoản) — rà soát lại thấy Name/NamHoc chưa có đường Admin sửa cho
        // người khác, chỉ tự sửa được qua PUT /me.
        group.MapPut("/users/{id:guid}/admin-edit", async (Guid id, AdminEditUserRequest request, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.AdminEditUserAsync(principal.GetRole(), id, request.Name, request.NamHoc, ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<AdminEditUserRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Gap 2 mục 3: nhật ký hoạt động của 1 Lớp — Admin luôn được, Teacher chỉ được nếu đúng là
        // GV chủ nhiệm của lopId đó (ownership check trong GetLopActivityLogAsync).
        group.MapGet("/lop/{id:guid}/activity-log", async (Guid id, int? top, ClaimsPrincipal principal, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.GetLopActivityLogAsync(id, principal.GetUserId(), principal.GetRole(), top is > 0 and <= 200 ? top.Value : 50, ct);
                return Results.Ok(ApiResponse<IReadOnlyList<LopActivityLogResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc 4.2 mục 3 (2026-08-19) — nội bộ, chỉ admin-service gọi khi dựng backup TRƯỚC khi
        // xóa toàn bộ dữ liệu Lớp. Bản KHÔNG cap top của /lop/{id}/activity-log ở trên — xem
        // remarks GetAllLopActivityLogInternalAsync.
        group.MapGet("/lop/{id:guid}/activity-log/all", async (Guid id, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.GetAllLopActivityLogInternalAsync(id, ct);
                return Results.Ok(ApiResponse<IReadOnlyList<LopActivityLogResponse>>.Ok(result));
            })
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Việc 4.2 mục 3 — XÓA VĨNH VIỄN, bước cuối trong saga cross-service. Chỉ admin-service gọi
        // (RequireInternalServiceKeyFilter, cùng nguyên tắc PUT /users/{id}/role) — Admin KHÔNG gọi
        // trực tiếp endpoint này qua gateway để đảm bảo LopDeletionService's audit+backup-trước-khi-
        // xóa luôn chạy trước, không có đường tắt bỏ qua backup.
        group.MapPost("/lop/{id:guid}/delete-all-data", async (Guid id, IKhoaLopService service, CancellationToken ct) =>
            {
                var result = await service.DeleteAllLopDataAsync(id, ct);
                return Results.Ok(ApiResponse<DeleteAllLopDataResponse>.Ok(result));
            })
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        return app;
    }
}
