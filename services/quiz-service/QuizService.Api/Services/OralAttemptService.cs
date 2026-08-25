using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;
using QuizService.Api.Grading;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Services;

public sealed class OralAttemptService(QuizDbContext db, IOralGradingClient gradingClient) : IOralAttemptService
{
    public async Task<OralResultResponse> SubmitAsync(Guid userId, SubmitOralRequest request, CancellationToken ct)
    {
        var question = await db.OralQuestions.FindAsync([request.QuestionId], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi vấn đáp.");

        var followups = request.FollowupAnswers ?? [];
        var grading = await gradingClient.GradeAsync(
            new OralGradingRequest(question.QuestionText, question.ExpectedAnswer, request.MainAnswer, followups), ct);

        var result = new OralResult
        {
            UserId = userId,
            QuestionId = question.Id,
            MainAnswer = request.MainAnswer,
            FollowupAnswersJson = followups.Count > 0 ? JsonSerializer.Serialize(followups) : null,
            AiScore = grading.Score,
            AiComment = grading.Comment,
            RubricScoresJson = grading.RubricScores is { Count: > 0 } ? JsonSerializer.Serialize(grading.RubricScores) : null,
        };

        // Việc 4.1 (2026-08-19) — gắn câu trả lời vào đúng phiên (nếu có). Vấn đáp không cần chấm
        // lại gì đặc biệt khi bị bỏ dở (mỗi câu đã tự chấm+lưu ngay khi trả lời, không có gì để
        // mất) — chỉ cần biết phiên đã HOÀN TẤT hay chưa. Nếu đây là câu TRẢ LỜI CUỐI CÙNG còn
        // thiếu của phiên (đủ số câu đã giao), tự đánh dấu Submitted — không cần 1 lời gọi "kết
        // thúc" riêng từ client.
        if (request.SessionId is { } sessionId)
        {
            var session = await db.ExamSessions.SingleOrDefaultAsync(
                s => s.Id == sessionId && s.UserId == userId && s.Kind == ExamSessionKind.VanDap && s.Status == ExamSessionStatus.InProgress, ct);
            if (session is not null)
            {
                result.ExamSessionId = session.Id;
                db.OralResults.Add(result);
                await db.SaveChangesAsync(ct);

                var assignedIds = JsonSerializer.Deserialize<List<Guid>>(session.QuestionIdsJson) ?? [];
                var answeredCount = await db.OralResults.CountAsync(r => r.ExamSessionId == session.Id, ct);
                if (answeredCount >= assignedIds.Count)
                {
                    session.Status = ExamSessionStatus.Submitted;
                    await db.SaveChangesAsync(ct);
                }

                return ToResponse(result);
            }
        }

        db.OralResults.Add(result);
        await db.SaveChangesAsync(ct);

        return ToResponse(result);
    }

    public async Task<IReadOnlyList<OralResultResponse>> GetMyResultsAsync(Guid userId, CancellationToken ct)
    {
        var results = await db.OralResults
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        return results.Select(ToResponse).ToList();
    }

    // ===================== Việc 4.1 (2026-08-19) — Chống thoát thi thử =====================

    public async Task<StartExamSessionResponse> StartOralSessionAsync(Guid userId, StartExamSessionRequest request, CancellationToken ct)
    {
        // Cùng bất biến "1 InProgress/user/kind" như TracNghiem — mở phiên vấn đáp mới luôn chốt
        // (đánh dấu bỏ dở) phiên InProgress cũ trước, không cho giữ nhiều phiên song song.
        var staleSessions = await db.ExamSessions
            .Where(s => s.UserId == userId && s.Kind == ExamSessionKind.VanDap && s.Status == ExamSessionStatus.InProgress)
            .ToListAsync(ct);
        foreach (var stale in staleSessions)
        {
            MarkAbandonedIfInProgress(stale);
        }
        if (staleSessions.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        var session = new ExamSession
        {
            UserId = userId,
            Kind = ExamSessionKind.VanDap,
            QuestionIdsJson = JsonSerializer.Serialize(request.QuestionIds),
            ExpectedDurationSeconds = request.ExpectedDurationSeconds,
        };
        db.ExamSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return new StartExamSessionResponse(session.Id);
    }

    public async Task AbandonOralSessionAsync(Guid userId, AbandonOralSessionRequest request, CancellationToken ct)
    {
        var session = await db.ExamSessions.SingleOrDefaultAsync(
            s => s.Id == request.SessionId && s.UserId == userId && s.Kind == ExamSessionKind.VanDap, ct);
        if (session is null)
        {
            return; // best-effort từ beacon — không có nơi báo lỗi.
        }

        MarkAbandonedIfInProgress(session);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Đã bị chốt bởi đường khác (VD lazy-check ở GetMySessionsAsync) trong lúc xử lý —
            // idempotent, bỏ qua thay vì lỗi.
        }
    }

    public async Task<IReadOnlyList<OralSessionResponse>> GetMySessionsAsync(Guid userId, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - AbandonGracePeriod;
        var sessions = await db.ExamSessions
            .Where(s => s.UserId == userId && s.Kind == ExamSessionKind.VanDap)
            .OrderByDescending(s => s.StartedAtUtc)
            .ToListAsync(ct);

        var changed = false;
        foreach (var session in sessions.Where(s => s.Status == ExamSessionStatus.InProgress))
        {
            if (session.StartedAtUtc.AddSeconds(session.ExpectedDurationSeconds) <= cutoff)
            {
                MarkAbandonedIfInProgress(session);
                changed = true;
            }
        }
        if (changed)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // idempotent — đã bị chốt song song bởi đường khác, bỏ qua.
            }
        }

        var answeredCounts = await db.OralResults
            .Where(r => r.ExamSessionId != null && sessions.Select(s => s.Id).Contains(r.ExamSessionId!.Value))
            .GroupBy(r => r.ExamSessionId!.Value)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, ct);

        return sessions.Select(s =>
        {
            var assignedIds = JsonSerializer.Deserialize<List<Guid>>(s.QuestionIdsJson) ?? [];
            answeredCounts.TryGetValue(s.Id, out var answered);
            return new OralSessionResponse(s.Id, s.Status, s.StartedAtUtc, assignedIds.Count, answered);
        }).ToList();
    }

    private static readonly TimeSpan AbandonGracePeriod = TimeSpan.FromMinutes(2);

    private static void MarkAbandonedIfInProgress(ExamSession session)
    {
        if (session.Status != ExamSessionStatus.InProgress)
        {
            return;
        }
        session.Status = ExamSessionStatus.AutoSubmittedAbandoned;
    }

    private static OralResultResponse ToResponse(OralResult r) => new(
        r.Id,
        r.QuestionId,
        r.MainAnswer,
        r.AiScore,
        r.AiComment,
        r.RubricScoresJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, decimal>>(r.RubricScoresJson),
        r.CreatedAtUtc);
}
