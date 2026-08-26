using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

public interface IQuizAttemptService
{
    Task<SubmitResultResponse> SubmitExamAsync(Guid userId, SubmitExamRequest request, CancellationToken ct);
    Task<IReadOnlyList<WrongAnswerResponse>> GetWrongAnswersAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<MyResultResponse>> GetMyResultsAsync(Guid userId, CancellationToken ct);

    // Việc 4.1 (2026-08-19) — chống thoát thi thử.
    Task<StartExamSessionResponse> StartExamSessionAsync(Guid userId, StartExamSessionRequest request, CancellationToken ct);
    Task AutoSubmitExamSessionAsync(Guid userId, AutoSubmitExamRequest request, CancellationToken ct);
}
