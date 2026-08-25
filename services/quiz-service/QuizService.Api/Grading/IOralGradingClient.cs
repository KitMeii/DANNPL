namespace QuizService.Api.Grading;

public sealed record OralGradingRequest(string QuestionText, string? ExpectedAnswer, string MainAnswer, IReadOnlyList<string> FollowupAnswers);

public sealed record OralGradingResult(decimal Score, string? Comment, IReadOnlyDictionary<string, decimal>? RubricScores);

/// <summary>
/// quiz-service never grades oral/vấn đáp answers itself — that call always goes to ai-service,
/// which is the only place a Groq API key is ever used (see [[project_microservices_migration]]
/// for why: the old frontend called Groq directly from the browser with a per-user key). This
/// interface is the contract; the HTTP implementation targets ai-service's future
/// POST /api/v1/ai/grade-oral endpoint and will 500 until ai-service exists — same forward-
/// reference situation as the gateway's not-yet-built cluster destinations.
/// </summary>
public interface IOralGradingClient
{
    Task<OralGradingResult> GradeAsync(OralGradingRequest request, CancellationToken ct);
}
