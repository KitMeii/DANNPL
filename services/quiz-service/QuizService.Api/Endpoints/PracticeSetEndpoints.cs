using System.Security.Claims;
using QuizService.Api.Clients;
using QuizService.Api.Dtos;
using QuizService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace QuizService.Api.Endpoints;

/// <summary>Việc 4.4 Phần B (2026-08-20) — "Đề luyện tập" giáo viên tạo, giao theo Lớp. Xem remarks
/// Entities/PracticeSet.cs cho thiết kế đầy đủ.</summary>
public static class PracticeSetEndpoints
{
    public static IEndpointRouteBuilder MapPracticeSetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quiz/practice-sets").WithTags("PracticeSets").RequireAuthorization();

        group.MapGet("/chapters", async (IPracticeSetService service, CancellationToken ct) =>
            {
                var result = await service.ListChapterOptionsAsync(ct);
                return Results.Ok(ApiResponse<IReadOnlyList<ChapterOptionResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPost("/", async (CreatePracticeSetRequest request, ClaimsPrincipal principal, IPracticeSetService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(request, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Created($"/api/v1/quiz/practice-sets/{result.Id}", ApiResponse<PracticeSetResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreatePracticeSetRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapGet("/mine", async (ClaimsPrincipal principal, IPracticeSetService service, CancellationToken ct) =>
            {
                var result = await service.ListMineAsync(principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<IReadOnlyList<PracticeSetResponse>>.Ok(result));
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, IPracticeSetService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(id, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Học viên (hoặc GV) xem đề khả dụng cho đúng Lớp của mình — không RequireRole tĩnh, cùng
        // nguyên tắc GET /questions/practice (RBAC thật phụ thuộc dữ liệu, nằm trong service).
        group.MapGet("/available", async (IAuthQuizClient authClient, IPracticeSetService service, CancellationToken ct) =>
        {
            var callerLopId = await authClient.GetMyLopIdAsync(ct);
            var result = await service.ListAvailableAsync(callerLopId, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<PracticeSetResponse>>.Ok(result));
        });

        return app;
    }
}
