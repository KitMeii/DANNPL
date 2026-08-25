using System.Security.Claims;
using AdminService.Api.Dtos;
using AdminService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace AdminService.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Every route here requires Admin — the gateway already double-checks this on
        // /api/v1/admin/**, but this service enforces it independently too (same convention as
        // every other service in this codebase: never trust the gateway alone).
        var group = app.MapGroup("/api/v1/admin").WithTags("Admin").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapGet("/users", async (string? role, Guid? lopId, Guid? khoaId, IUserManagementService service, CancellationToken ct) =>
        {
            var result = await service.ListUsersAsync(role, lopId, khoaId, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<UserSummaryResponse>>.Ok(result));
        });

        group.MapPut("/users/{id:guid}/role", async (Guid id, ChangeRoleRequest request, ClaimsPrincipal principal, IUserManagementService service, CancellationToken ct) =>
            {
                var result = await service.ChangeRoleAsync(principal.GetUserId(), id, request.Role, ct);
                return Results.Ok(ApiResponse<UserSummaryResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<ChangeRoleRequest>>();

        // Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp (Role bất kỳ), không qua
        // /register (luôn tạo Student).
        group.MapPost("/users", async (CreateUserRequest request, IUserManagementService service, CancellationToken ct) =>
            {
                var result = await service.CreateUserAsync(request.Email, request.Password, request.Name, request.Role, ct);
                return Results.Created($"/api/v1/admin/users/{result.Id}", ApiResponse<UserSummaryResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateUserRequest>>();

        // Rà soát Lần XII (2026-08-21) — khóa/mở khóa tài khoản (chặn đăng nhập, giữ nguyên dữ
        // liệu). Chặn tự khóa chính mình được kiểm ở auth-service (cùng vị trí "không thể tự đổi
        // role" — ChangeRoleAsync ở trên cũng KHÔNG tự kiểm ở tầng này, để auth-service làm nguồn
        // sự thật duy nhất cho ràng buộc này).
        group.MapPut("/users/{id:guid}/locked", async (Guid id, SetUserLockedRequest request, IUserManagementService service, CancellationToken ct) =>
            {
                var result = await service.SetUserLockedAsync(id, request.IsLocked, ct);
                return Results.Ok(ApiResponse<UserSummaryResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<SetUserLockedRequest>>();

        group.MapGet("/audit-log", async (int? top, IUserManagementService service, CancellationToken ct) =>
        {
            var result = await service.GetAuditLogAsync(top is > 0 and <= 200 ? top.Value : 50, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<RoleChangeAuditResponse>>.Ok(result));
        });

        group.MapGet("/config", async (ISystemConfigService service, CancellationToken ct) =>
        {
            var result = await service.GetAllAsync(ct);
            return Results.Ok(ApiResponse<IReadOnlyList<SystemConfigResponse>>.Ok(result));
        });

        group.MapPut("/config/{key}", async (string key, SetConfigRequest request, ClaimsPrincipal principal, ISystemConfigService service, CancellationToken ct) =>
            {
                var result = await service.SetAsync(key, request.Value, principal.GetUserId(), ct);
                return Results.Ok(ApiResponse<SystemConfigResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<SetConfigRequest>>();

        group.MapGet("/stats/overview", async (ISystemOverviewService service, CancellationToken ct) =>
        {
            var result = await service.GetOverviewAsync(ct);
            return Results.Ok(ApiResponse<SystemOverviewResponse>.Ok(result));
        });

        // Việc 7 (2026-08-16) — Dashboard "Theo dõi Giáo viên". Chỉ liệt kê (sắp theo tên), KHÔNG
        // so sánh/xếp hạng hiệu suất giữa các giáo viên — quyết định nghiệp vụ nhạy cảm, cố ý bỏ
        // qua theo yêu cầu (xem báo cáo Việc 7).
        group.MapGet("/stats/teachers", async (ITeacherOverviewService service, CancellationToken ct) =>
        {
            var result = await service.GetTeacherOverviewAsync(ct);
            return Results.Ok(ApiResponse<IReadOnlyList<TeacherOverviewResponse>>.Ok(result));
        });

        group.MapGet("/stats/questions-by-chapter", async (ITeacherOverviewService service, CancellationToken ct) =>
        {
            var result = await service.GetQuestionCountsByChapterAsync(ct);
            return Results.Ok(ApiResponse<IReadOnlyList<ChapterQuestionCountResponse>>.Ok(result));
        });

        // Việc D (2026-08-16) — drill-down: từng Lớp của 1 giáo viên kèm điểm TB riêng (khác
        // /stats/teachers ở trên, vốn gộp mọi Lớp của giáo viên thành 1 con số).
        group.MapGet("/stats/teachers/{teacherId:guid}/lop-quality", async (Guid teacherId, ITeacherOverviewService service, CancellationToken ct) =>
        {
            var result = await service.GetTeacherLopQualityAsync(teacherId, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<LopQualityResponse>>.Ok(result));
        });

        group.MapGet("/stats/khoa/{khoaId:guid}", async (Guid khoaId, ILopKhoaStatsService service, CancellationToken ct) =>
        {
            var result = await service.GetKhoaStatsAsync(khoaId, ct);
            return Results.Ok(ApiResponse<KhoaDiemTrungBinhResponse>.Ok(result));
        });

        // Gap 2: Teacher HOẶC Admin xem điểm TB Lớp — đăng ký thẳng trên `app`, NGOÀI `group` ở
        // trên, vì nhiều RequireAuthorization trên cùng 1 endpoint kết hợp theo AND chứ không phải
        // OR: nếu để trong group (đã RequireRole(Admin)) rồi gọi RequireAuthorization(Teacher,
        // Admin) lần nữa, kết quả net vẫn là Admin-only (giao của "Admin" và "Teacher OR Admin" =
        // chỉ Admin), không đạt mục đích nới quyền cho Teacher. Phần "đúng GV chủ nhiệm lopId đó"
        // là data-dependent nên nằm trong LopKhoaStatsService.GetLopStatsAsync (tái dùng nguyên
        // logic tổng hợp điểm tách thi thử/luyện tập đã build ở Bước C, không viết lại) — service
        // đã gọi authClient.GetLopAsync để lấy tên Lớp sẵn, tận dụng luôn GiaoVienId trả về để
        // kiểm tra quyền, không tốn thêm 1 lần gọi mạng.
        app.MapGet("/api/v1/admin/stats/lop/{lopId:guid}", async (Guid lopId, ClaimsPrincipal principal, ILopKhoaStatsService service, CancellationToken ct) =>
            {
                var result = await service.GetLopStatsAsync(lopId, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<LopDiemTrungBinhResponse>.Ok(result));
            })
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc 4.2 mục 3 (2026-08-19) — xóa toàn bộ dữ liệu 1 Lớp (hủy diệt, KHÔNG khôi phục).
        // Cả 2 endpoint dưới đây nằm trong `group` ở trên nên đã Admin-only — Teacher (kể cả GV chủ
        // nhiệm của chính lớp đó) KHÔNG gọi được, đúng yêu cầu "chỉ Admin, không phải Teacher".
        group.MapPost("/lop/{id:guid}/prepare-deletion", async (Guid id, ClaimsPrincipal principal, ILopDeletionService service, CancellationToken ct) =>
        {
            var result = await service.PrepareAsync(id, principal.GetUserId(), ct);
            return Results.Ok(ApiResponse<PrepareLopDeletionResponse>.Ok(result));
        });

        group.MapPost("/lop/{id:guid}/execute-deletion", async (Guid id, ExecuteLopDeletionRequest request, ClaimsPrincipal principal, ILopDeletionService service, CancellationToken ct) =>
            {
                var result = await service.ExecuteAsync(id, request, principal.GetUserId(), ct);
                return Results.Ok(ApiResponse<ExecuteLopDeletionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<ExecuteLopDeletionRequest>>();

        group.MapGet("/lop-deletion-audits", async (int? top, ILopDeletionService service, CancellationToken ct) =>
        {
            var result = await service.GetAuditHistoryAsync(top is > 0 and <= 200 ? top.Value : 50, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<LopDeletionAuditResponse>>.Ok(result));
        });

        return app;
    }
}
