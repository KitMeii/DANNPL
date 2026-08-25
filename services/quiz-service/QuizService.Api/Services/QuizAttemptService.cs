using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;
using QuizService.Api.Progress;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Services;

/// <summary>
/// Server-side grading. This is the fix for audit finding F3: the old frontend fetched the full
/// answer key to the browser, graded itself, and just wrote whatever score it computed straight
/// to the database — a student could fabricate a perfect score from devtools without answering
/// anything. Here the client sends only its selected options; the correct answers never leave
/// this service until after grading, and the score is always computed from the stored Question
/// rows, never trusted from the request.
/// </summary>
public sealed class QuizAttemptService(QuizDbContext db, IProgressReporter progressReporter, ILogger<QuizAttemptService> logger) : IQuizAttemptService
{
    // Việc 4.1 (2026-08-19) — sau thời lượng dự kiến của phiên + khoảng đệm này mà vẫn InProgress
    // thì lazy-check coi là bỏ dở. Đệm 2 phút chống false-positive do lệch giờ nhỏ/độ trễ mạng,
    // không phải cơ chế chính (cơ chế chính là beacon lúc rời trang, xem AutoSubmitExamSessionAsync).
    private static readonly TimeSpan AbandonGracePeriod = TimeSpan.FromMinutes(2);

    public async Task<SubmitResultResponse> SubmitPracticeAsync(Guid userId, SubmitQuizRequest request, CancellationToken ct)
    {
        var (result, gradedAnswers) = await GradeAsync(request.Answers, ct);

        db.QuizResults.Add(new QuizResult
        {
            UserId = userId,
            Chapter = request.Chapter,
            Score = result.Score,
            Correct = result.Correct,
            Total = result.Total,
        });

        await RecordWrongAnswersAsync(userId, gradedAnswers, ct);
        await db.SaveChangesAsync(ct);
        await ReportScoreBestEffortAsync(userId, result.Score, ct);

        return result;
    }

    public async Task<SubmitResultResponse> SubmitExamAsync(Guid userId, SubmitExamRequest request, CancellationToken ct)
    {
        var (result, gradedAnswers) = await GradeAsync(request.Answers, ct);

        var examResult = new ExamResult
        {
            UserId = userId,
            Score = result.Score,
            Correct = result.Correct,
            Total = result.Total,
            TimeSpentSeconds = request.TimeSpentSeconds,
            IsAutoSubmitted = false,
        };

        // Việc 4.1 — gắn kết quả vào đúng phiên (nếu có) để lazy-check sau này không còn thấy
        // phiên ở trạng thái InProgress nữa, tránh chốt trùng. SessionId không hợp lệ/đã bị chốt
        // bởi đường khác (cực hiếm, xem AbandonGracePeriod) thì vẫn cho nộp bình thường, chỉ không
        // gắn được session — không chặn học viên nộp bài hợp lệ.
        if (request.SessionId is { } sessionId)
        {
            var session = await db.ExamSessions.SingleOrDefaultAsync(
                s => s.Id == sessionId && s.UserId == userId && s.Status == ExamSessionStatus.InProgress, ct);
            if (session is not null)
            {
                examResult.ExamSessionId = session.Id;
                session.Status = ExamSessionStatus.Submitted;
                session.ExamResultId = examResult.Id;
            }
        }

        db.ExamResults.Add(examResult);
        await RecordWrongAnswersAsync(userId, gradedAnswers, ct);
        await db.SaveChangesAsync(ct);
        await ReportScoreBestEffortAsync(userId, result.Score, ct);

        return result;
    }

    // ===================== Việc 4.1 (2026-08-19) — Chống thoát thi thử =====================

    public async Task<StartExamSessionResponse> StartExamSessionAsync(Guid userId, StartExamSessionRequest request, CancellationToken ct)
    {
        // Bất biến "1 InProgress/user/kind" — chốt NGAY mọi phiên TracNghiem InProgress cũ của
        // đúng user này TRƯỚC KHI tạo phiên mới, bất kể phiên cũ mới mở được bao lâu. Đây là điểm
        // chặn chính lỗ hổng "mở nhiều phiên song song, chỉ nộp phiên tốt" — mở phiên mới luôn
        // "tốn" phiên cũ, không có cách nào giữ nhiều phiên InProgress cùng lúc.
        var staleSessions = await db.ExamSessions
            .Where(s => s.UserId == userId && s.Kind == ExamSessionKind.TracNghiem && s.Status == ExamSessionStatus.InProgress)
            .ToListAsync(ct);
        foreach (var stale in staleSessions)
        {
            await FinalizeSessionCoreAsync(stale, answers: [], ct);
        }

        var session = new ExamSession
        {
            UserId = userId,
            Kind = ExamSessionKind.TracNghiem,
            QuestionIdsJson = JsonSerializer.Serialize(request.QuestionIds),
            ExpectedDurationSeconds = request.ExpectedDurationSeconds,
        };
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return new StartExamSessionResponse(session.Id);
    }

    public async Task AutoSubmitExamSessionAsync(Guid userId, AutoSubmitExamRequest request, CancellationToken ct)
    {
        // Không tồn tại/không phải của user này — im lặng bỏ qua. Đây là lời gọi best-effort từ
        // navigator.sendBeacon() lúc rời trang, không có nơi hiển thị lỗi cho người dùng.
        var session = await db.ExamSessions.SingleOrDefaultAsync(
            s => s.Id == request.SessionId && s.UserId == userId && s.Kind == ExamSessionKind.TracNghiem, ct);
        if (session is null)
        {
            return;
        }

        await FinalizeSessionCoreAsync(session, request.Answers, ct);
    }

    /// <summary>Lõi dùng chung cho CẢ 2 đường chốt phiên bỏ dở: Lớp 1 (beacon, answers là dữ liệu
    /// thật trong bộ nhớ JS lúc rời trang) và Lớp 2 (lazy-check ở StartExamSessionAsync/
    /// GetMyResultsAsync, answers luôn rỗng vì không có gì để lấy — beacon không tới nơi được).
    /// Idempotent qua RowVersion (concurrency token trên ExamSession): nếu 2 lời gọi cùng cố chốt
    /// 1 session (hiếm — VD beacon và lazy-check xảy ra gần như đồng thời), request ghi trước
    /// thắng; request thua gặp DbUpdateConcurrencyException, hiểu là "đã bị chốt rồi", bỏ qua thay
    /// vì tạo thêm 1 dòng ExamResult trùng.</summary>
    private async Task FinalizeSessionCoreAsync(ExamSession session, IReadOnlyList<SubmitAnswerItem> answers, CancellationToken ct)
    {
        if (session.Status != ExamSessionStatus.InProgress)
        {
            return;
        }

        var allQuestionIds = JsonSerializer.Deserialize<List<Guid>>(session.QuestionIdsJson) ?? [];
        var (result, gradedAnswers) = await GradeAgainstFullPoolAsync(allQuestionIds, answers, ct);

        var examResult = new ExamResult
        {
            UserId = session.UserId,
            Score = result.Score,
            Correct = result.Correct,
            Total = result.Total,
            TimeSpentSeconds = session.ExpectedDurationSeconds,
            IsAutoSubmitted = true,
            ExamSessionId = session.Id,
        };

        db.ExamResults.Add(examResult);
        session.Status = ExamSessionStatus.AutoSubmittedAbandoned;
        session.ExamResultId = examResult.Id;
        await RecordWrongAnswersAsync(session.UserId, gradedAnswers, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ExamResults.Remove(examResult);
            db.Entry(session).State = EntityState.Detached;
            logger.LogInformation(
                "ExamSession {SessionId} đã được chốt bởi 1 lời gọi khác trong lúc xử lý — bỏ qua (idempotent, không tạo ExamResult trùng).",
                session.Id);
            return;
        }

        await ReportScoreBestEffortAsync(session.UserId, result.Score, ct);
    }

    /// <summary>Chấm theo TOÀN BỘ bộ câu hỏi được giao lúc bắt đầu phiên (denominator =
    /// allQuestionIds.Count) — khác GradeAsync thường (denominator = answers.Count, dùng cho nộp
    /// tay bình thường, xem confirmSubmitTN() ở thi-thu.html: câu chưa làm KHÔNG tính vào bài chấm
    /// khi học viên tự bấm Nộp). Ở đây thì NGƯỢC LẠI có chủ đích — câu chưa trả lời tính sai (0
    /// điểm), vì đây là hậu quả của việc BỎ DỞ chứ không phải chủ động nộp thiếu.</summary>
    private async Task<(SubmitResultResponse Result, IReadOnlyList<GradedAnswer> GradedAnswers)> GradeAgainstFullPoolAsync(
        IReadOnlyList<Guid> allQuestionIds, IReadOnlyList<SubmitAnswerItem> answers, CancellationToken ct)
    {
        if (allQuestionIds.Count == 0)
        {
            return (new SubmitResultResponse(0, 0, 0, []), []);
        }

        var questions = await db.Questions.Where(q => allQuestionIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var answeredById = answers.ToDictionary(a => a.QuestionId, a => a.SelectedOption);

        var graded = new List<GradedAnswer>(allQuestionIds.Count);
        var correctCount = 0;

        foreach (var questionId in allQuestionIds)
        {
            if (!questions.TryGetValue(questionId, out var question))
            {
                // Câu hỏi bị xóa khỏi ngân hàng giữa lúc học viên đang thi — bỏ qua, không chấm
                // được nữa (cực hiếm, không đáng chặn cả phiên vì lý do này).
                continue;
            }

            var hasAnswer = answeredById.TryGetValue(questionId, out var selected);
            var isCorrect = hasAnswer && selected == question.CorrectAnswer;
            if (isCorrect)
            {
                correctCount++;
            }

            // -1 = chưa trả lời — cùng quy ước sentinel đã dùng ở thi-thu.html (tnAnswers.fill(-1)).
            graded.Add(new GradedAnswer(question.Id, hasAnswer ? selected : -1, question.CorrectAnswer, isCorrect, question.Explanation));
        }

        var total = graded.Count;
        var score = total > 0 ? Math.Round(correctCount * 10m / total, 2) : 0m;
        return (new SubmitResultResponse(score, correctCount, total, graded), graded);
    }

    private async Task ReportScoreBestEffortAsync(Guid userId, decimal score, CancellationToken ct)
    {
        try
        {
            await progressReporter.ReportScoreAsync(score, ct);
        }
        catch (Exception ex)
        {
            // progress-service tracks the leaderboard/average — secondary to the grading result
            // itself, so a temporary outage there must not fail the student's quiz submission.
            logger.LogWarning(ex, "Failed to report score to progress-service for user {UserId}", userId);
        }
    }

    public async Task<IReadOnlyList<WrongAnswerResponse>> GetWrongAnswersAsync(Guid userId, CancellationToken ct)
    {
        var rows = await (
            from wrong in db.WrongAnswers
            join question in db.Questions on wrong.QuestionId equals question.Id
            where wrong.UserId == userId
            orderby wrong.WrongCount descending, wrong.LastWrongAtUtc descending
            select new WrongAnswerResponse(question.Id, question.QuestionText, question.Chapter, wrong.WrongCount, wrong.LastWrongAtUtc)
        ).ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<MyResultResponse>> GetMyResultsAsync(Guid userId, CancellationToken ct)
    {
        // Lớp 2 (lazy-check) — chốt bất kỳ phiên nào đã quá thời lượng dự kiến + khoảng đệm, phòng
        // trường hợp beacon (Lớp 1) không kịp gửi (crash, mất mạng, tắt máy đột ngột).
        await FinalizeExpiredSessionsAsync(userId, ct);

        var quizResults = await db.QuizResults.Where(r => r.UserId == userId)
            .Select(r => new MyResultResponse(r.Id, "practice", r.Chapter, r.Score, r.Correct, r.Total, r.CreatedAtUtc, false))
            .ToListAsync(ct);

        var examResults = await db.ExamResults.Where(r => r.UserId == userId)
            .Select(r => new MyResultResponse(r.Id, "exam", null, r.Score, r.Correct, r.Total, r.CreatedAtUtc, r.IsAutoSubmitted))
            .ToListAsync(ct);

        return quizResults.Concat(examResults).OrderByDescending(r => r.CreatedAtUtc).ToList();
    }

    private async Task FinalizeExpiredSessionsAsync(Guid userId, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - AbandonGracePeriod;
        var candidates = await db.ExamSessions
            .Where(s => s.UserId == userId && s.Kind == ExamSessionKind.TracNghiem && s.Status == ExamSessionStatus.InProgress)
            .ToListAsync(ct);

        foreach (var session in candidates)
        {
            if (session.StartedAtUtc.AddSeconds(session.ExpectedDurationSeconds) <= cutoff)
            {
                await FinalizeSessionCoreAsync(session, answers: [], ct);
            }
        }
    }

    private async Task<(SubmitResultResponse Result, IReadOnlyList<GradedAnswer> GradedAnswers)> GradeAsync(
        IReadOnlyList<SubmitAnswerItem> answers, CancellationToken ct)
    {
        var questionIds = answers.Select(a => a.QuestionId).Distinct().ToList();
        var questions = await db.Questions.Where(q => questionIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);

        var missing = questionIds.Where(id => !questions.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException($"Không tìm thấy {missing.Count} câu hỏi trong bài nộp.");
        }

        var graded = new List<GradedAnswer>(answers.Count);
        var correctCount = 0;

        foreach (var answer in answers)
        {
            var question = questions[answer.QuestionId];
            var isCorrect = answer.SelectedOption == question.CorrectAnswer;
            if (isCorrect)
            {
                correctCount++;
            }

            graded.Add(new GradedAnswer(question.Id, answer.SelectedOption, question.CorrectAnswer, isCorrect, question.Explanation));
        }

        var total = answers.Count;
        var score = total > 0 ? Math.Round(correctCount * 10m / total, 2) : 0m;

        return (new SubmitResultResponse(score, correctCount, total, graded), graded);
    }

    private async Task RecordWrongAnswersAsync(Guid userId, IReadOnlyList<GradedAnswer> gradedAnswers, CancellationToken ct)
    {
        var wrongQuestionIds = gradedAnswers.Where(a => !a.IsCorrect).Select(a => a.QuestionId).ToList();
        if (wrongQuestionIds.Count == 0)
        {
            return;
        }

        var existing = await db.WrongAnswers
            .Where(w => w.UserId == userId && wrongQuestionIds.Contains(w.QuestionId))
            .ToDictionaryAsync(w => w.QuestionId, ct);

        foreach (var questionId in wrongQuestionIds)
        {
            if (existing.TryGetValue(questionId, out var wrongAnswer))
            {
                wrongAnswer.WrongCount++;
                wrongAnswer.LastWrongAtUtc = DateTime.UtcNow;
            }
            else
            {
                db.WrongAnswers.Add(new WrongAnswer { UserId = userId, QuestionId = questionId, WrongCount = 1, LastWrongAtUtc = DateTime.UtcNow });
            }
        }
    }
}
