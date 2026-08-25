using System.Security.Claims;
using QuizService.Api.Dtos;
using QuizService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace QuizService.Api.Endpoints;

public static class ExamSetEndpoints
{
    public static IEndpointRouteBuilder MapExamSetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quiz/exam-sets").WithTags("ExamSets").RequireAuthorization();

        // Rà soát Lần XI (2026-08-21) — GV chỉ thấy/thao tác Bộ đề CHÍNH MÌNH tạo (Admin thấy hết),
        // cùng lỗi đã sửa cho Question/OralQuestion/Material ở Lần VIII nhưng ExamSet bị bỏ sót.
        group.MapGet("/", async (ClaimsPrincipal principal, IExamSetService service, CancellationToken ct) =>
            {
                var result = await service.ListAsync(principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<IReadOnlyList<ExamSetSummaryResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, IExamSetService service, CancellationToken ct) =>
            {
                var result = await service.GetByIdAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<ExamSetResponse>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPost("/generate", async (GenerateExamSetVersionsRequest request, ClaimsPrincipal principal, IExamSetService service, CancellationToken ct) =>
            {
                var result = await service.GenerateAsync(request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Created($"/api/v1/quiz/exam-sets/{result.Id}", ApiResponse<ExamSetResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<GenerateExamSetVersionsRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc 5 (2026-08-16) — "Bộ đề VĐ mới" từ ngân hàng câu hỏi vấn đáp có sẵn.
        group.MapPost("/generate-oral", async (GenerateOralExamSetVersionsRequest request, ClaimsPrincipal principal, IExamSetService service, CancellationToken ct) =>
            {
                var result = await service.GenerateOralAsync(request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Created($"/api/v1/quiz/exam-sets/{result.Id}", ApiResponse<ExamSetResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<GenerateOralExamSetVersionsRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPut("/versions/{versionId:guid}/publish", async (Guid versionId, ClaimsPrincipal principal, IExamSetService service, CancellationToken ct) =>
            {
                var count = await service.PublishVersionAsync(versionId, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<PublishVersionResponse>.Ok(new PublishVersionResponse(count)));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPut("/versions/{versionId:guid}/unpublish", async (Guid versionId, ClaimsPrincipal principal, IExamSetService service, CancellationToken ct) =>
            {
                var count = await service.UnpublishVersionAsync(versionId, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<UnpublishVersionResponse>.Ok(new UnpublishVersionResponse(count)));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc 8 (2026-08-16) — sửa lại phạm vi hiển thị của 1 mã đề đã có.
        group.MapPut("/versions/{versionId:guid}/lop-visibility", async (Guid versionId, UpdateExamVersionLopVisibilityRequest request, ClaimsPrincipal principal, IExamSetService service, CancellationToken ct) =>
            {
                var result = await service.UpdateVersionLopVisibilityAsync(versionId, request.LopIds, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<ExamVersionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateExamVersionLopVisibilityRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        return app;
    }
}
