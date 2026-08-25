using QuizService.Api.Grading;

namespace QuizService.Tests.Integration;

/// <summary>Stands in for ai-service (which doesn't exist yet) so oral-submission tests can run
/// without a network call. Always returns a fixed score so tests can assert on it deterministically.</summary>
public sealed class FakeOralGradingClient : IOralGradingClient
{
    public const decimal FixedScore = 7.5m;

    public Task<OralGradingResult> GradeAsync(OralGradingRequest request, CancellationToken ct) =>
        Task.FromResult(new OralGradingResult(FixedScore, "Trả lời khá tốt.", new Dictionary<string, decimal> { ["content"] = 8m, ["clarity"] = 7m }));
}
