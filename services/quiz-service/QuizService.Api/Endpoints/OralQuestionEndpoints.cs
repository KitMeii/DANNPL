using System.Security.Claims;
using QuizService.Api.Clients;
using QuizService.Api.Dtos;
using QuizService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace QuizService.Api.Endpoints;

public static class OralQuestionEndpoints
{
    public static IEndpointRouteBuilder MapOralQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quiz/oral-questions").WithTags("OralQuestions").RequireAuthorization();

        // Rà soát Lần VIII (2026-08-21) — cùng lý do QuestionEndpoints: mỗi GV chỉ thấy câu vấn đáp
        // chính mình tạo, Admin thấy hết.
        group.MapGet("/", async (string? chapter, ClaimsPrincipal principal, IOralQuestionService service, CancellationToken ct) =>
            {
                var result = await service.ListAsync(chapter, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<IReadOnlyList<OralQuestionResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc 4.4 Phần A (2026-08-20) — lọc theo Lớp người gọi, cùng pattern GET /questions/practice.
        group.MapGet("/practice", async (string? chapter, IAuthQuizClient authClient, IOralQuestionService service, CancellationToken ct) =>
        {
            var callerLopId = await authClient.GetMyLopIdAsync(ct);
            var result = await service.ListForPracticeAsync(chapter, callerLopId, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<OralQuestionPracticeResponse>>.Ok(result));
        });

        group.MapPost("/", async (CreateOralQuestionRequest request, ClaimsPrincipal principal, IOralQuestionService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Created($"/api/v1/quiz/oral-questions/{result.Id}", ApiResponse<OralQuestionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateOralQuestionRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPut("/{id:guid}", async (Guid id, UpdateOralQuestionRequest request, ClaimsPrincipal principal, IOralQuestionService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(id, request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<OralQuestionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateOralQuestionRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, IOralQuestionService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc 4.4 Phần A — sửa lại phạm vi hiển thị của 1 câu hỏi vấn đáp đã có. Rà soát Lần VIII —
        // thêm kiểm tra ownership câu hỏi (chỉ người tạo/Admin), không chỉ ownership Lớp như trước.
        group.MapPut("/{id:guid}/lop-visibility", async (Guid id, UpdateOralQuestionLopVisibilityRequest request, ClaimsPrincipal principal, IOralQuestionService service, CancellationToken ct) =>
            {
                var result = await service.UpdateLopVisibilityAsync(id, request.LopIds, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<OralQuestionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateOralQuestionLopVisibilityRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        return app;
    }
}
