using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

public interface IOralAttemptService
{
    Task<OralResultResponse> SubmitAsync(Guid userId, SubmitOralRequest request, CancellationToken ct);
    Task<IReadOnlyList<OralResultResponse>> GetMyResultsAsync(Guid userId, CancellationToken ct);

    // Việc 4.1 (2026-08-19) — chống thoát thi thử.
    Task<StartExamSessionResponse> StartOralSessionAsync(Guid userId, StartExamSessionRequest request, CancellationToken ct);
    Task AbandonOralSessionAsync(Guid userId, AbandonOralSessionRequest request, CancellationToken ct);
    Task<IReadOnlyList<OralSessionResponse>> GetMySessionsAsync(Guid userId, CancellationToken ct);
}
