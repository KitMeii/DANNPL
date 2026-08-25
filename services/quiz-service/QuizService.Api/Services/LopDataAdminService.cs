using Microsoft.EntityFrameworkCore;
using QuizService.Api.Data;
using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

public sealed class LopDataAdminService(QuizDbContext db) : ILopDataAdminService
{
    public async Task<LopDataDumpResponse> DumpAsync(LopDataRequest request, CancellationToken ct)
    {
        var userIds = request.UserIds;

        var quizResults = await db.QuizResults.Where(r => userIds.Contains(r.UserId))
            .Select(r => new QuizResultDump(r.Id, r.UserId, r.Chapter, r.Score, r.Correct, r.Total, r.CreatedAtUtc))
            .ToListAsync(ct);

        var examResults = await db.ExamResults.Where(r => userIds.Contains(r.UserId))
            .Select(r => new ExamResultDump(r.Id, r.UserId, r.Score, r.IsAutoSubmitted, r.ExamSessionId, r.CreatedAtUtc))
            .ToListAsync(ct);

        var examSessions = await db.ExamSessions.Where(s => userIds.Contains(s.UserId))
            .Select(s => new ExamSessionDump(s.Id, s.UserId, s.Kind, s.Status, s.StartedAtUtc, s.ExpectedDurationSeconds, s.ExamResultId))
            .ToListAsync(ct);

        var oralResults = await db.OralResults.Where(r => userIds.Contains(r.UserId))
            .Select(r => new OralResultDump(r.Id, r.UserId, r.QuestionId, r.AiScore, r.ExamSessionId, r.CreatedAtUtc))
            .ToListAsync(ct);

        var wrongAnswers = await db.WrongAnswers.Where(w => userIds.Contains(w.UserId))
            .Select(w => new WrongAnswerDump(w.UserId, w.QuestionId, w.WrongCount, w.LastWrongAtUtc))
            .ToListAsync(ct);

        var questionVisibility = await db.QuestionLopVisibilities.Where(v => v.LopId == request.LopId)
            .Select(v => v.QuestionId).ToListAsync(ct);

        var essayVisibility = await db.EssayQuestionLopVisibilities.Where(v => v.LopId == request.LopId)
            .Select(v => v.EssayQuestionId).ToListAsync(ct);

        var examVersionVisibility = await db.ExamVersionLopVisibilities.Where(v => v.LopId == request.LopId)
            .Select(v => v.ExamVersionId).ToListAsync(ct);

        return new LopDataDumpResponse(quizResults, examResults, examSessions, oralResults, wrongAnswers,
            questionVisibility, essayVisibility, examVersionVisibility);
    }

    public async Task<LopDataDeleteResponse> DeleteAsync(LopDataRequest request, CancellationToken ct)
    {
        var userIds = request.UserIds;

        var quizResultsDeleted = await db.QuizResults.Where(r => userIds.Contains(r.UserId)).ExecuteDeleteAsync(ct);
        var examResultsDeleted = await db.ExamResults.Where(r => userIds.Contains(r.UserId)).ExecuteDeleteAsync(ct);
        var examSessionsDeleted = await db.ExamSessions.Where(s => userIds.Contains(s.UserId)).ExecuteDeleteAsync(ct);
        var oralResultsDeleted = await db.OralResults.Where(r => userIds.Contains(r.UserId)).ExecuteDeleteAsync(ct);
        var wrongAnswersDeleted = await db.WrongAnswers.Where(w => userIds.Contains(w.UserId)).ExecuteDeleteAsync(ct);

        var questionVisibilityDeleted = await db.QuestionLopVisibilities.Where(v => v.LopId == request.LopId).ExecuteDeleteAsync(ct);
        var essayVisibilityDeleted = await db.EssayQuestionLopVisibilities.Where(v => v.LopId == request.LopId).ExecuteDeleteAsync(ct);
        var examVersionVisibilityDeleted = await db.ExamVersionLopVisibilities.Where(v => v.LopId == request.LopId).ExecuteDeleteAsync(ct);

        return new LopDataDeleteResponse(quizResultsDeleted, examResultsDeleted, examSessionsDeleted, oralResultsDeleted,
            wrongAnswersDeleted, questionVisibilityDeleted, essayVisibilityDeleted, examVersionVisibilityDeleted);
    }
}
