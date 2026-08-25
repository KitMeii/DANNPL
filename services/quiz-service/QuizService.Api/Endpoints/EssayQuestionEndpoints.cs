using System.Security.Claims;
using QuizService.Api.Clients;
using QuizService.Api.Dtos;
using QuizService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace QuizService.Api.Endpoints;

public static class EssayQuestionEndpoints
{
    public static IEndpointRouteBuilder MapEssayQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quiz/essay-questions").WithTags("EssayQuestions").RequireAuthorization();

        // Rà soát Lần XI (2026-08-21) — GV chỉ thấy/thao tác câu tự luận CHÍNH MÌNH tạo (Admin thấy
        // hết), cùng lỗi đã sửa cho Question/OralQuestion/Material ở Lần VIII nhưng EssayQuestion bị
        // bỏ sót hoàn toàn (trước đây List không lọc, Update/Delete/Publish không có cách nào kiểm).
        group.MapGet("/", async (string? chapter, ClaimsPrincipal principal, IEssayQuestionService service, CancellationToken ct) =>
            {
                var result = await service.ListAsync(chapter, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<IReadOnlyList<EssayQuestionResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapGet("/practice", async (string? chapter, IAuthQuizClient authClient, IEssayQuestionService service, CancellationToken ct) =>
        {
            var callerLopId = await authClient.GetMyLopIdAsync(ct);
            var result = await service.ListForPracticeAsync(chapter, callerLopId, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<EssayQuestionPracticeResponse>>.Ok(result));
        });

        group.MapPost("/", async (CreateEssayQuestionRequest request, ClaimsPrincipal principal, IEssayQuestionService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Created($"/api/v1/quiz/essay-questions/{result.Id}", ApiResponse<EssayQuestionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateEssayQuestionRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPut("/{id:guid}", async (Guid id, UpdateEssayQuestionRequest request, ClaimsPrincipal principal, IEssayQuestionService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(id, request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<EssayQuestionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateEssayQuestionRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, IEssayQuestionService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPut("/{id:guid}/publish", async (Guid id, ClaimsPrincipal principal, IEssayQuestionService service, CancellationToken ct) =>
            {
                var result = await service.TogglePublishAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<EssayQuestionResponse>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc 8 (2026-08-16) — sửa lại phạm vi hiển thị của 1 câu tự luận đã có.
        group.MapPut("/{id:guid}/lop-visibility", async (Guid id, UpdateEssayQuestionLopVisibilityRequest request, ClaimsPrincipal principal, IEssayQuestionService service, CancellationToken ct) =>
            {
                var result = await service.UpdateLopVisibilityAsync(id, request.LopIds, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<EssayQuestionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateEssayQuestionLopVisibilityRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        return app;
    }
}
