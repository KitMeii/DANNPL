using System.Security.Claims;
using AuthService.Api.Dtos;
using AuthService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace AuthService.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService authService, CancellationToken ct) =>
            {
                var result = await authService.RegisterAsync(request, ct);
                return Results.Created($"/api/v1/auth/me", ApiResponse<AuthResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<RegisterRequest>>()
            .AllowAnonymous();

        group.MapPost("/login", async (LoginRequest request, IAuthService authService, CancellationToken ct) =>
            {
                var result = await authService.LoginAsync(request, ct);
                return Results.Ok(ApiResponse<AuthResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<LoginRequest>>()
            .AllowAnonymous();

        group.MapGet("/me", async (ClaimsPrincipal principal, IAuthService authService, CancellationToken ct) =>
            {
                var result = await authService.GetByIdAsync(principal.GetUserId(), ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .RequireAuthorization();

        group.MapPut("/me", async (UpdateProfileRequest request, ClaimsPrincipal principal, IAuthService authService, CancellationToken ct) =>
            {
                var result = await authService.UpdateProfileAsync(principal.GetUserId(), request, ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateProfileRequest>>()
            .RequireAuthorization();

        // Đổi avatar — userId luôn lấy từ JWT (principal.GetUserId()), không nhận id khác từ
        // client, nên mọi user (Student/Teacher/Admin) chỉ đổi được avatar của chính mình, cùng
        // pattern với GET /lop/mine. Validate loại/dung lượng ở đây (inline, giống
        // MaterialEndpoints.cs's /upload) thay vì FluentValidation vì input là IFormFile, không
        // phải JSON DTO.
        group.MapPost("/me/avatar", async (IFormFile file, ClaimsPrincipal principal, IAuthService authService, CancellationToken ct) =>
            {
                const long maxFileSizeBytes = 5 * 1024 * 1024;
                var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp" };

                if (file.Length == 0)
                {
                    throw new FluentValidation.ValidationException("File rỗng.");
                }

                if (file.Length > maxFileSizeBytes)
                {
                    throw new FluentValidation.ValidationException("Ảnh vượt quá 5MB.");
                }

                if (!allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                {
                    throw new FluentValidation.ValidationException("Chỉ chấp nhận ảnh JPG, PNG hoặc WebP.");
                }

                await using var stream = file.OpenReadStream();
                var result = await authService.UpdateAvatarAsync(principal.GetUserId(), stream, file.FileName, ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .DisableAntiforgery()
            .RequireAuthorization();

        // Rà soát Lần VI (2026-08-21) — đổi mật khẩu self-service, mọi role. userId luôn lấy từ JWT
        // (không nhận id khác từ client), cùng pattern /me/avatar ở trên.
        group.MapPut("/me/password", async (ChangePasswordRequest request, ClaimsPrincipal principal, IAuthService authService, CancellationToken ct) =>
            {
                await authService.ChangePasswordAsync(principal.GetUserId(), request.CurrentPassword, request.NewPassword, ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .AddEndpointFilter<ValidationEndpointFilter<ChangePasswordRequest>>()
            .RequireAuthorization();

        // Cross-service display enrichment (progress-service leaderboard, admin-service roster) —
        // name only, see UserNameResponse remarks. Any authenticated caller, not just Teacher/Admin,
        // since a student's own leaderboard view needs classmates' names too.
        group.MapGet("/users/names", async (string ids, IAuthService authService, CancellationToken ct) =>
            {
                var parsedIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Guid.Parse)
                    .ToList();
                var result = await authService.GetNamesByIdsAsync(parsedIds, ct);
                return Results.Ok(ApiResponse<IReadOnlyList<UserNameResponse>>.Ok(result));
            })
            .RequireAuthorization();

        // Gap 2 mục 2 (Hướng B): Teacher/Admin tìm học viên theo email để gán vào Lớp. Trả tối
        // thiểu (Id/Name/Email/LopId) — xem StudentSearchResponse + SearchStudentsByEmailAsync
        // remarks. KHÔNG cần RequireInternalServiceKeyFilter như /users bên dưới vì đây không phải
        // audit-sensitive account management, chỉ là tra cứu tối thiểu phục vụ gán lớp.
        group.MapGet("/users/search-by-email", async (string email, IAuthService authService, CancellationToken ct) =>
            {
                var result = await authService.SearchStudentsByEmailAsync(email, ct);
                return Results.Ok(ApiResponse<IReadOnlyList<StudentSearchResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Admin-only account management. Intended caller is admin-service (which audits every
        // change) — see the remarks on admin-service's HttpAuthAdminClient. [Authorize(Roles=Admin)]
        // alone is not enough: the gateway proxies every sub-path under /api/v1/auth/**, so any
        // client with a valid Admin JWT could otherwise call these directly and bypass
        // admin-service's audit log. RequireInternalServiceKeyFilter closes that: only a caller
        // that also knows InternalService:SharedKey (admin-service) gets through.
        group.MapGet("/users", async (string? role, Guid? lopId, Guid? khoaId, IAuthService authService, CancellationToken ct) =>
            {
                var result = await authService.ListUsersAsync(role, lopId, khoaId, ct);
                return Results.Ok(ApiResponse<IReadOnlyList<UserResponse>>.Ok(result));
            })
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapPut("/users/{id:guid}/role", async (Guid id, ChangeRoleRequest request, ClaimsPrincipal principal, IAuthService authService, CancellationToken ct) =>
            {
                if (principal.GetUserId() == id)
                {
                    throw new ConflictException("Không thể tự đổi role của chính mình.");
                }

                var result = await authService.ChangeRoleAsync(id, request.Role, ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<ChangeRoleRequest>>()
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp (Role bất kỳ), không qua
        // /register (luôn tạo Student). Cùng cơ chế bảo vệ RequireInternalServiceKeyFilter với các
        // endpoint quản lý tài khoản khác — chỉ admin-service (đã audit) mới gọi được.
        group.MapPost("/users", async (CreateUserByAdminRequest request, IAuthService authService, CancellationToken ct) =>
            {
                var result = await authService.CreateUserByAdminAsync(request, ct);
                return Results.Created($"/api/v1/auth/users/{result.Id}", ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateUserByAdminRequest>>()
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        // Rà soát Lần XII (2026-08-21) — khóa/mở khóa tài khoản (chặn đăng nhập, giữ nguyên dữ
        // liệu) — xem User.IsLocked remarks. Chặn tự khóa chính mình, cùng vị trí kiểm tra với
        // "không thể tự đổi role" ở trên.
        group.MapPut("/users/{id:guid}/locked", async (Guid id, SetUserLockedRequest request, ClaimsPrincipal principal, IAuthService authService, CancellationToken ct) =>
            {
                if (principal.GetUserId() == id)
                {
                    throw new ConflictException("Không thể tự khóa chính mình.");
                }

                var result = await authService.SetUserLockedAsync(id, request.IsLocked, ct);
                return Results.Ok(ApiResponse<UserResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<SetUserLockedRequest>>()
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        return app;
    }
}
