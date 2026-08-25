using AiService.Api.AiProviders;
using AiService.Api.Dtos;
using AiService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace AiService.Api.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai").WithTags("Ai").RequireAuthorization();

        group.MapPost("/chat", async (ChatRequest request, IChatService service, CancellationToken ct) =>
                await RunAiAwareAsync(() => service.ChatAsync(request, ct)))
            .AddEndpointFilter<ValidationEndpointFilter<ChatRequest>>();

        group.MapPost("/generate-lecture", async (GenerateLectureRequest request, ILectureService service, CancellationToken ct) =>
                await RunAiAwareAsync(() => service.GenerateLectureAsync(request, ct)))
            .AddEndpointFilter<ValidationEndpointFilter<GenerateLectureRequest>>();

        group.MapPost("/generate-comprehension-questions", async (GenerateComprehensionQuestionsRequest request, ILectureService service, CancellationToken ct) =>
                await RunAiAwareAsync(() => service.GenerateComprehensionQuestionsAsync(request, ct)))
            .AddEndpointFilter<ValidationEndpointFilter<GenerateComprehensionQuestionsRequest>>();

        // Called by quiz-service (service-to-service, not by the frontend directly) to grade a
        // vấn đáp answer — see QuizService.Api/Grading/IOralGradingClient.cs on the caller side.
        group.MapPost("/grade-oral", async (GradeOralRequest request, IOralGradingService service, CancellationToken ct) =>
                await RunAiAwareAsync(() => service.GradeAsync(request, ct)))
            .AddEndpointFilter<ValidationEndpointFilter<GradeOralRequest>>();

        group.MapPost("/extract-questions", async (ExtractQuestionsRequest request, IQuestionExtractionService service, CancellationToken ct) =>
                await RunAiAwareAsync(() => service.ExtractAsync(request, ct)))
            .AddEndpointFilter<ValidationEndpointFilter<ExtractQuestionsRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        group.MapPost("/generate-exam-set", async (GenerateExamSetRequest request, IQuestionExtractionService service, CancellationToken ct) =>
                await RunAiAwareAsync(() => service.GenerateExamSetAsync(request, ct)))
            .AddEndpointFilter<ValidationEndpointFilter<GenerateExamSetRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // "Kiểm tra nhanh kiến thức" (audit 2026-08-16 mục 3) — Student tự kiểm tra khi xem tài
        // liệu, KHÔNG lưu DB, KHÔNG qua duyệt. Không giới hạn role (giống generate-lecture/
        // generate-comprehension-questions) — chỉ cần đăng nhập, mọi role gọi được vì không ai
        // trong số đó có đường nào lưu kết quả xuống ngân hàng câu hỏi chính thức từ endpoint này.
        group.MapPost("/quick-check", async (QuickCheckRequest request, IQuestionExtractionService service, CancellationToken ct) =>
                await RunAiAwareAsync(() => service.QuickCheckAsync(request, ct)))
            .AddEndpointFilter<ValidationEndpointFilter<QuickCheckRequest>>();

        return app;
    }

    // Two error shapes can reach here: AllAiProvidersFailedException (from AiProviderRouter's
    // CompleteTextAsync/CompleteJsonAsync, after retry+failover across every configured provider
    // was exhausted) and a raw AiProviderException (from ChatService, which calls
    // aiRouter.ChatAsync — bypasses the router's retry/failover, see AiProviderRouter.ChatAsync
    // remarks, so its provider's exception reaches here directly). Both map to the same HTTP shape
    // instead of falling through to the generic 500 "Đã xảy ra lỗi hệ thống" that
    // ExceptionHandlingMiddleware would otherwise produce, so callers (e.g. giang-bai.html's
    // chunked lecture generation) can distinguish "wait and retry" from a real failure.
    private static async Task<IResult> RunAiAwareAsync<TResponse>(Func<Task<TResponse>> action)
    {
        try
        {
            var result = await action();
            return Results.Ok(ApiResponse<TResponse>.Ok(result));
        }
        catch (AllAiProvidersFailedException ex)
        {
            return MapAiFailure<TResponse>(ex.Message, ex.RetryAfterSeconds);
        }
        catch (AiProviderTransientException ex)
        {
            return MapAiFailure<TResponse>(ex.Message, ex.RetryAfterSeconds);
        }
        catch (AiProviderPermanentException ex)
        {
            return MapAiFailure<TResponse>(ex.Message, retryAfterSeconds: null);
        }
    }

    // retryAfterSeconds present = worth the caller waiting and retrying the SAME request (429).
    // Null = retrying won't help on its own (payload too large, all providers down) — 503 instead,
    // so a caller like giang-bai.html's chunk retry loop doesn't waste attempts on something that
    // can never succeed by simply waiting.
    private static IResult MapAiFailure<TResponse>(string message, double? retryAfterSeconds)
    {
        var isTransient = retryAfterSeconds is not null;
        var status = isTransient ? StatusCodes.Status429TooManyRequests : StatusCodes.Status503ServiceUnavailable;
        var code = isTransient ? ErrorCodes.RateLimited : ErrorCodes.AiUnavailable;
        return Results.Json(ApiResponse<TResponse>.Fail(code, message, retryAfterSeconds), statusCode: status);
    }
}
