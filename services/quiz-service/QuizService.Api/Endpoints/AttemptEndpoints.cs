using System.Security.Claims;
using QuizService.Api.Dtos;
using QuizService.Api.Services;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace QuizService.Api.Endpoints;

public static class AttemptEndpoints
{
    public static IEndpointRouteBuilder MapAttemptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quiz").WithTags("Attempts").RequireAuthorization();

        group.MapPost("/practice/submit", async (SubmitQuizRequest request, ClaimsPrincipal principal, IQuizAttemptService service, CancellationToken ct) =>
            {
                var result = await service.SubmitPracticeAsync(principal.GetUserId(), request, ct);
                return Results.Ok(ApiResponse<SubmitResultResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<SubmitQuizRequest>>();

        group.MapPost("/exams/submit", async (SubmitExamRequest request, ClaimsPrincipal principal, IQuizAttemptService service, CancellationToken ct) =>
            {
                var result = await service.SubmitExamAsync(principal.GetUserId(), request, ct);
                return Results.Ok(ApiResponse<SubmitResultResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<SubmitExamRequest>>();

        // Việc 4.1 (2026-08-19) — chống thoát thi thử (trắc nghiệm). Gọi TRƯỚC khi hiển thị câu hỏi
        // cho học viên, ngay sau khi client fetch xong bộ câu hỏi.
        group.MapPost("/exams/start", async (StartExamSessionRequest request, ClaimsPrincipal principal, IQuizAttemptService service, CancellationToken ct) =>
            {
                var result = await service.StartExamSessionAsync(principal.GetUserId(), request, ct);
                return Results.Ok(ApiResponse<StartExamSessionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<StartExamSessionRequest>>();

        // Lớp 1 — mục tiêu chính của navigator.sendBeacon() lúc học viên rời trang giữa chừng.
        // Không trả lỗi rõ ràng cho client (sendBeacon không đọc response) — luôn 200 nếu request
        // hợp lệ về hình thức, phần "session có thật không / còn InProgress không" xử lý im lặng
        // ở service (xem AutoSubmitExamSessionAsync remarks).
        group.MapPost("/exams/auto-submit", async (AutoSubmitExamRequest request, ClaimsPrincipal principal, IQuizAttemptService service, CancellationToken ct) =>
            {
                await service.AutoSubmitExamSessionAsync(principal.GetUserId(), request, ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .AddEndpointFilter<ValidationEndpointFilter<AutoSubmitExamRequest>>();

        group.MapGet("/wrong-answers", async (ClaimsPrincipal principal, IQuizAttemptService service, CancellationToken ct) =>
        {
            var result = await service.GetWrongAnswersAsync(principal.GetUserId(), ct);
            return Results.Ok(ApiResponse<IReadOnlyList<WrongAnswerResponse>>.Ok(result));
        });

        group.MapGet("/my-results", async (ClaimsPrincipal principal, IQuizAttemptService service, CancellationToken ct) =>
        {
            var result = await service.GetMyResultsAsync(principal.GetUserId(), ct);
            return Results.Ok(ApiResponse<IReadOnlyList<MyResultResponse>>.Ok(result));
        });

        group.MapPost("/oral/submit", async (SubmitOralRequest request, ClaimsPrincipal principal, IOralAttemptService service, CancellationToken ct) =>
            {
                var result = await service.SubmitAsync(principal.GetUserId(), request, ct);
                return Results.Ok(ApiResponse<OralResultResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<SubmitOralRequest>>();

        group.MapGet("/oral/results", async (ClaimsPrincipal principal, IOralAttemptService service, CancellationToken ct) =>
        {
            var result = await service.GetMyResultsAsync(principal.GetUserId(), ct);
            return Results.Ok(ApiResponse<IReadOnlyList<OralResultResponse>>.Ok(result));
        });

        // Việc 4.1 (2026-08-19) — chống thoát thi thử (vấn đáp). Vấn đáp tuyến tính, không có
        // navigator câu hỏi (quyết định 2026-08-19 — giữ đúng bản chất hỏi-đáp tuần tự).
        group.MapPost("/oral/start", async (StartExamSessionRequest request, ClaimsPrincipal principal, IOralAttemptService service, CancellationToken ct) =>
            {
                var result = await service.StartOralSessionAsync(principal.GetUserId(), request, ct);
                return Results.Ok(ApiResponse<StartExamSessionResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<StartExamSessionRequest>>();

        // Lớp 1 — sendBeacon lúc rời trang. Vấn đáp không mất dữ liệu khi thoát (mỗi câu đã lưu
        // ngay lúc trả lời qua /oral/submit) nên chỉ cần đánh dấu phiên bị bỏ dở, không cần gửi
        // answers như /exams/auto-submit.
        group.MapPost("/oral/abandon", async (AbandonOralSessionRequest request, ClaimsPrincipal principal, IOralAttemptService service, CancellationToken ct) =>
            {
                await service.AbandonOralSessionAsync(principal.GetUserId(), request, ct);
                return Results.Ok(ApiResponse.Ok());
            })
            .AddEndpointFilter<ValidationEndpointFilter<AbandonOralSessionRequest>>();

        group.MapGet("/oral/sessions", async (ClaimsPrincipal principal, IOralAttemptService service, CancellationToken ct) =>
        {
            var result = await service.GetMySessionsAsync(principal.GetUserId(), ct);
            return Results.Ok(ApiResponse<IReadOnlyList<OralSessionResponse>>.Ok(result));
        });

        return app;
    }
}
