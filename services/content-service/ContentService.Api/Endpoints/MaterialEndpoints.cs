using System.Security.Claims;
using ContentService.Api.Dtos;
using ContentService.Api.Services;
using ContentService.Api.Storage;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace ContentService.Api.Endpoints;

public static class MaterialEndpoints
{
    public static IEndpointRouteBuilder MapMaterialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/content/materials").WithTags("Materials").RequireAuthorization();

        // Rà soát Lần VIII (2026-08-21) — thêm callerUserId/callerRole để Teacher chỉ thấy tài liệu
        // chính mình tải lên (xem remarks MaterialService.ListAsync) — Student/Admin không đổi.
        group.MapGet("/", async (ClaimsPrincipal principal, string? chapter, IMaterialService service, CancellationToken ct) =>
        {
            var includeInactive = IsTeacherOrAdmin(principal);
            var result = await service.ListAsync(includeInactive, chapter, principal.GetUserId(), principal.GetRole(), ct);
            return Results.Ok(ApiResponse<IReadOnlyList<MaterialResponse>>.Ok(result));
        });

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, IMaterialService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, IsTeacherOrAdmin(principal), ct);
            return Results.Ok(ApiResponse<MaterialResponse>.Ok(result));
        });

        // Raw file upload — server-side, so the browser never holds a storage API key (Cloudinary
        // replaces the old client-side Supabase Storage upload, see CloudinaryFileStorage
        // remarks). Only the URL/publicId/metadata this returns are ever sent back to the client;
        // the client then POSTs those into CreateMaterialRequest below to save the record.
        group.MapPost("/upload", async (IFormFile file, IFileStorage storage, CancellationToken ct) =>
            {
                const long maxFileSizeBytes = 50 * 1024 * 1024;
                if (file.Length == 0)
                {
                    throw new FluentValidation.ValidationException("File rỗng.");
                }

                if (file.Length > maxFileSizeBytes)
                {
                    throw new FluentValidation.ValidationException("File vượt quá 50MB.");
                }

                if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FluentValidation.ValidationException("Chỉ chấp nhận file PDF.");
                }

                await using var stream = file.OpenReadStream();
                var uploaded = await storage.UploadAsync(stream, file.FileName, ct);
                var result = new UploadedFileResponse(uploaded.Url, file.FileName, uploaded.FileSize, uploaded.PublicId);
                return Results.Ok(ApiResponse<UploadedFileResponse>.Ok(result));
            })
            .DisableAntiforgery()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPost("/", async (CreateMaterialRequest request, ClaimsPrincipal principal, IMaterialService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(request, principal.GetUserId(), ct);
                return Results.Created($"/api/v1/content/materials/{result.Id}", ApiResponse<MaterialResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateMaterialRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPut("/{id:guid}", async (Guid id, UpdateMaterialRequest request, ClaimsPrincipal principal, IMaterialService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(id, request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<MaterialResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateMaterialRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, IMaterialService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPost("/{id:guid}/view", async (Guid id, IMaterialService service, CancellationToken ct) =>
        {
            var viewCount = await service.IncrementViewCountAsync(id, ct);
            return Results.Ok(ApiResponse<int>.Ok(viewCount));
        });

        return app;
    }

    private static bool IsTeacherOrAdmin(ClaimsPrincipal principal) =>
        principal.IsInRole(Roles.Teacher) || principal.IsInRole(Roles.Admin);
}
