using Microsoft.EntityFrameworkCore;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;
using Shared.Contracts;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Services;

public sealed class OralQuestionService(QuizDbContext db, ILopScopeGuard lopScopeGuard) : IOralQuestionService
{
    public async Task<IReadOnlyList<OralQuestionResponse>> ListAsync(string? chapter, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var query = db.OralQuestions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(chapter))
        {
            query = query.Where(q => q.Chapter == chapter);
        }

        // Rà soát Lần VIII (2026-08-21) — Teacher chỉ thấy câu của chính mình, Admin thấy hết.
        if (callerRole != Roles.Admin)
        {
            query = query.Where(q => q.CreatedBy == callerUserId);
        }

        var questions = await query.OrderBy(q => q.Chapter).ThenByDescending(q => q.CreatedAtUtc).ToListAsync(ct);
        var lopIdsByQuestion = await LoadLopIdsAsync(questions.Select(q => q.Id).ToList(), ct);
        return questions.Select(q => ToResponse(q, lopIdsByQuestion)).ToList();
    }

    public async Task<IReadOnlyList<OralQuestionPracticeResponse>> ListForPracticeAsync(string? chapter, Guid? callerLopId, CancellationToken ct)
    {
        var query = db.OralQuestions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(chapter))
        {
            query = query.Where(q => q.Chapter == chapter);
        }

        // Việc 4.4 Phần A: cùng công thức QuestionService.ListForPracticeAsync — 0 dòng
        // OralQuestionLopVisibility = toàn hệ thống, có dòng = chỉ khớp đúng Lớp người gọi. Áp dụng
        // cho CẢ luyện tập vấn đáp lẫn pool câu hỏi thi vấn đáp (student/thi-thu.html gọi chung
        // endpoint này qua getOralQuestions()).
        return await query
            .Where(q => !db.OralQuestionLopVisibilities.Any(v => v.OralQuestionId == q.Id) ||
                        (callerLopId != null && db.OralQuestionLopVisibilities.Any(v => v.OralQuestionId == q.Id && v.LopId == callerLopId)))
            .OrderBy(q => q.CreatedAtUtc)
            .Select(q => new OralQuestionPracticeResponse(q.Id, q.Chapter, q.QuestionText, q.Difficulty))
            .ToListAsync(ct);
    }

    public async Task<OralQuestionResponse> CreateAsync(CreateOralQuestionRequest request, Guid createdBy, string callerRole, CancellationToken ct)
    {
        var lopIds = (request.LopIds ?? []).Distinct().ToList();
        await lopScopeGuard.EnsureCanAssignAsync(lopIds, callerRole, ct);

        var question = new OralQuestion
        {
            Chapter = request.Chapter?.Trim(),
            QuestionText = request.QuestionText.Trim(),
            ExpectedAnswer = request.ExpectedAnswer?.Trim(),
            Difficulty = request.Difficulty,
            CreatedBy = createdBy,
        };

        db.OralQuestions.Add(question);
        foreach (var lopId in lopIds)
        {
            db.OralQuestionLopVisibilities.Add(new OralQuestionLopVisibility { OralQuestionId = question.Id, LopId = lopId });
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(question, lopIds);
    }

    public async Task<OralQuestionResponse> UpdateAsync(Guid id, UpdateOralQuestionRequest request, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.OralQuestions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi vấn đáp.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        question.Chapter = request.Chapter?.Trim();
        question.QuestionText = request.QuestionText.Trim();
        question.ExpectedAnswer = request.ExpectedAnswer?.Trim();
        question.Difficulty = request.Difficulty;

        await db.SaveChangesAsync(ct);
        var lopIds = await db.OralQuestionLopVisibilities.Where(v => v.OralQuestionId == id).Select(v => v.LopId).ToListAsync(ct);
        return ToResponse(question, lopIds);
    }

    public async Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.OralQuestions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi vấn đáp.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        // Students' graded oral results (score, AI comment, rubric breakdown — the whole point of
        // the earlier fix that stopped this detail from being lost) reference this question. A
        // hard delete here would silently destroy that history, so refuse instead of cascading.
        var hasResults = await db.OralResults.AnyAsync(r => r.QuestionId == id, ct);
        if (hasResults)
        {
            throw new ConflictException("Không thể xoá câu hỏi đã có học viên trả lời — đã có kết quả chấm điểm gắn với câu hỏi này.");
        }

        db.OralQuestions.Remove(question);
        await db.SaveChangesAsync(ct);
    }

    public async Task<OralQuestionResponse> UpdateLopVisibilityAsync(Guid id, List<Guid> lopIds, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.OralQuestions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi vấn đáp.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        var distinctLopIds = lopIds.Distinct().ToList();
        var existingLopIds = await db.OralQuestionLopVisibilities.Where(v => v.OralQuestionId == id).Select(v => v.LopId).ToListAsync(ct);
        var touchedLopIds = distinctLopIds.Union(existingLopIds).ToList();
        await lopScopeGuard.EnsureCanAssignAsync(touchedLopIds, callerRole, ct);

        var current = await db.OralQuestionLopVisibilities.Where(v => v.OralQuestionId == id).ToListAsync(ct);
        db.OralQuestionLopVisibilities.RemoveRange(current);
        foreach (var lopId in distinctLopIds)
        {
            db.OralQuestionLopVisibilities.Add(new OralQuestionLopVisibility { OralQuestionId = id, LopId = lopId });
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(question, distinctLopIds);
    }

    private async Task<Dictionary<Guid, List<Guid>>> LoadLopIdsAsync(List<Guid> oralQuestionIds, CancellationToken ct)
    {
        var rows = await db.OralQuestionLopVisibilities.Where(v => oralQuestionIds.Contains(v.OralQuestionId)).ToListAsync(ct);
        return rows.GroupBy(v => v.OralQuestionId).ToDictionary(g => g.Key, g => g.Select(v => v.LopId).ToList());
    }

    private static OralQuestionResponse ToResponse(OralQuestion q, Dictionary<Guid, List<Guid>> lopIdsByQuestion) =>
        ToResponse(q, lopIdsByQuestion.TryGetValue(q.Id, out var lopIds) ? lopIds : []);

    private static OralQuestionResponse ToResponse(OralQuestion q, List<Guid> lopIds) =>
        new(q.Id, q.Chapter, q.QuestionText, q.ExpectedAnswer, q.Difficulty, q.CreatedBy, q.CreatedAtUtc, lopIds);

    /// <summary>Rà soát Lần VIII (2026-08-21) — cùng pattern QuestionService.EnsureOwnerOrAdmin.</summary>
    private static void EnsureOwnerOrAdmin(OralQuestion question, Guid callerUserId, string callerRole)
    {
        if (callerRole == Roles.Admin) return;
        if (question.CreatedBy == callerUserId) return;
        throw new UnauthorizedAccessException("Bạn chỉ được sửa/xóa câu hỏi vấn đáp do chính mình tạo.");
    }
}
