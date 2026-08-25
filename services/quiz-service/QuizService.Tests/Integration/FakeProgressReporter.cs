using QuizService.Api.Progress;

namespace QuizService.Tests.Integration;

public sealed class FakeProgressReporter : IProgressReporter
{
    public List<decimal> ReportedScores { get; } = [];

    public Task ReportScoreAsync(decimal score, CancellationToken ct)
    {
        ReportedScores.Add(score);
        return Task.CompletedTask;
    }
}
