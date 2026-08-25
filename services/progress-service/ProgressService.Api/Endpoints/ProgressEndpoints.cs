using System.Security.Claims;
using ProgressService.Api.Dtos;
using ProgressService.Api.Services;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace ProgressService.Api.Endpoints;

public static class ProgressEndpoints
{
    public static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/progress").WithTags("Progress").RequireAuthorization();

        group.MapPost("/study-logs", async (LogStudyTimeRequest request, ClaimsPrincipal principal, IStudyLogService service, CancellationToken ct) =>
            {
                await service.LogTodayAsync(principal.GetUserId(), request.Minutes, ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .AddEndpointFilter<ValidationEndpointFilter<LogStudyTimeRequest>>();

        group.MapGet("/study-logs/weekly", async (ClaimsPrincipal principal, IStudyLogService service, CancellationToken ct) =>
        {
            var result = await service.GetWeeklyAsync(principal.GetUserId(), ct);
            return Results.Ok(ApiResponse<IReadOnlyList<StudyLogResponse>>.Ok(result));
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, IStudentProgressService service, CancellationToken ct) =>
        {
            var result = await service.GetMyProgressAsync(principal.GetUserId(), ct);
            return Results.Ok(ApiResponse<MyProgressResponse>.Ok(result));
        });

        group.MapGet("/leaderboard", async (int? top, IStudentProgressService service, CancellationToken ct) =>
        {
            var result = await service.GetLeaderboardAsync(top is > 0 and <= 100 ? top.Value : 30, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<LeaderboardEntryResponse>>.Ok(result));
        });

        // Called by quiz-service right after grading a practice/exam attempt — see
        // QuizService.Api's HttpProgressReporter. The score is trusted here because quiz-service
        // is the one that actually computed it (never the browser) — but a forwarded student JWT
        // alone doesn't prove the call came from quiz-service rather than the student calling
        // this directly through the gateway's /api/v1/progress/** catch-all with a fabricated
        // Score (repeat of the F3 client-trusted-scoring finding, one hop over). Require
        // X-Internal-Key too, known only to quiz-service (Phần A RBAC audit finding).
        group.MapPost("/record-score", async (RecordScoreRequest request, ClaimsPrincipal principal, IStudentProgressService service, CancellationToken ct) =>
            {
                await service.RecordScoreAsync(principal.GetUserId(), request.Score, ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .AddEndpointFilter<ValidationEndpointFilter<RecordScoreRequest>>()
            .AddEndpointFilter<RequireInternalServiceKeyFilter>();

        return app;
    }
}
