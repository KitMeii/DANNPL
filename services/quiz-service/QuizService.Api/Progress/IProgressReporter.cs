namespace QuizService.Api.Progress;

/// <summary>Reports a graded score to progress-service so it can maintain the running average
/// used by the leaderboard. Deliberately best-effort at the call site (QuizAttemptService) —
/// progress tracking is a secondary side effect, not something that should fail a student's
/// quiz submission if progress-service happens to be down.</summary>
public interface IProgressReporter
{
    Task ReportScoreAsync(decimal score, CancellationToken ct);
}
