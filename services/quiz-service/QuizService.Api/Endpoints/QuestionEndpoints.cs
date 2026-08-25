using System.Security.Claims;
using QuizService.Api.Clients;
using QuizService.Api.Dtos;
using QuizService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace QuizService.Api.Endpoints;

public static class QuestionEndpoints
{
    public static IEndpointRouteBuilder MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quiz/questions").WithTags("Questions").RequireAuthorization();

        // Rà soát Lần VIII (2026-08-21) — mỗi GV CHỈ thấy câu hỏi CHÍNH MÌNH tạo (Admin thấy hết,
        // giữ vai trò giám sát toàn hệ thống) — trước đây hoàn toàn dùng chung, GV này thấy/sửa/xóa
        // được câu của GV khác, xác nhận là lỗi thật qua yêu cầu người dùng kiểm tra lại.
        group.MapGet("/", async (string? chapter, ClaimsPrincipal principal, IQuestionService service, CancellationToken ct) =>
            {
                var result = await service.ListAsync(chapter, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<IReadOnlyList<QuestionResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapGet("/practice", async (string? chapter, IAuthQuizClient authClient, IQuestionService service, CancellationToken ct) =>
        {
            var callerLopId = await authClient.GetMyLopIdAsync(ct);
            var result = await service.ListForPracticeAsync(chapter, callerLopId, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<QuizQuestionResponse>>.Ok(result));
        });

        group.MapPost("/", async (CreateQuestionRequest request, ClaimsPrincipal principal, IQuestionService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Created($"/api/v1/quiz/questions/{result.Id}", ApiResponse<QuestionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateQuestionRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPut("/{id:guid}", async (Guid id, UpdateQuestionRequest request, ClaimsPrincipal principal, IQuestionService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(id, request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<QuestionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateQuestionRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, IQuestionService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPut("/{id:guid}/publish", async (Guid id, ClaimsPrincipal principal, IQuestionService service, CancellationToken ct) =>
            {
                var result = await service.TogglePublishAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<QuestionResponse>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc 8 (2026-08-16) — sửa lại phạm vi hiển thị của 1 câu hỏi đã có (kể cả câu tạo trước
        // Việc 8, mặc định toàn hệ thống). Rà soát Lần VIII (2026-08-21) — thêm kiểm tra ownership
        // CÂU HỎI (chỉ người tạo/Admin), TRƯỚC đây chỉ kiểm ownership của LỚP được gán (LopScopeGuard,
        // vẫn giữ nguyên) — 1 GV không tạo câu này trước đây vẫn có thể gán/gỡ Lớp của chính GV đó
        // khỏi câu người khác, nay chặn luôn.
        group.MapPut("/{id:guid}/lop-visibility", async (Guid id, UpdateQuestionLopVisibilityRequest request, ClaimsPrincipal principal, IQuestionService service, CancellationToken ct) =>
            {
                var result = await service.UpdateLopVisibilityAsync(id, request.LopIds, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<QuestionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateQuestionLopVisibilityRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        return app;
    }
}
