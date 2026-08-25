using Microsoft.EntityFrameworkCore;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;
using Shared.Contracts;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Services;

public sealed class EssayQuestionService(QuizDbContext db, ILopScopeGuard lopScopeGuard) : IEssayQuestionService
{
    public async Task<IReadOnlyList<EssayQuestionResponse>> ListAsync(string? chapter, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var query = db.EssayQuestions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(chapter))
        {
            query = query.Where(q => q.Chapter == chapter);
        }

        if (callerRole != Roles.Admin)
        {
            query = query.Where(q => q.CreatedBy == callerUserId);
        }

        var questions = await query.OrderBy(q => q.Chapter).ThenByDescending(q => q.CreatedAtUtc).ToListAsync(ct);
        var lopIdsByQuestion = await LoadLopIdsAsync(questions.Select(q => q.Id).ToList(), ct);
        return questions.Select(q => ToResponse(q, lopIdsByQuestion)).ToList();
    }

    public async Task<IReadOnlyList<EssayQuestionPracticeResponse>> ListForPracticeAsync(string? chapter, Guid? callerLopId, CancellationToken ct)
    {
        var query = db.EssayQuestions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(chapter))
        {
            query = query.Where(q => q.Chapter == chapter);
        }

        return await query
            .Where(q => q.IsPublishedForPractice)
            .Where(q => !db.EssayQuestionLopVisibilities.Any(v => v.EssayQuestionId == q.Id) ||
                        (callerLopId != null && db.EssayQuestionLopVisibilities.Any(v => v.EssayQuestionId == q.Id && v.LopId == callerLopId)))
            .OrderBy(q => q.CreatedAtUtc)
            .Select(q => new EssayQuestionPracticeResponse(q.Id, q.Chapter, q.QuestionText))
            .ToListAsync(ct);
    }

    public async Task<EssayQuestionResponse> CreateAsync(CreateEssayQuestionRequest request, Guid createdBy, string callerRole, CancellationToken ct)
    {
        var lopIds = (request.LopIds ?? []).Distinct().ToList();
        await lopScopeGuard.EnsureCanAssignAsync(lopIds, callerRole, ct);

        var question = new EssayQuestion
        {
            Chapter = request.Chapter?.Trim(),
            QuestionText = request.QuestionText.Trim(),
            SuggestedAnswer = request.SuggestedAnswer?.Trim(),
            CreatedBy = createdBy,
            SourceType = request.SourceType,
            SourceMaterialId = request.SourceMaterialId,
            IsPublishedForPractice = request.SourceType == "Manual",
        };

        db.EssayQuestions.Add(question);
        foreach (var lopId in lopIds)
        {
            db.EssayQuestionLopVisibilities.Add(new EssayQuestionLopVisibility { EssayQuestionId = question.Id, LopId = lopId });
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(question, lopIds);
    }

    public async Task<EssayQuestionResponse> UpdateAsync(Guid id, UpdateEssayQuestionRequest request, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.EssayQuestions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi tự luận.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        question.Chapter = request.Chapter?.Trim();
        question.QuestionText = request.QuestionText.Trim();
        question.SuggestedAnswer = request.SuggestedAnswer?.Trim();

        await db.SaveChangesAsync(ct);
        var lopIds = await db.EssayQuestionLopVisibilities.Where(v => v.EssayQuestionId == id).Select(v => v.LopId).ToListAsync(ct);
        return ToResponse(question, lopIds);
    }

    public async Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.EssayQuestions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi tự luận.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        db.EssayQuestions.Remove(question);
        await db.SaveChangesAsync(ct);
    }

    public async Task<EssayQuestionResponse> TogglePublishAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.EssayQuestions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi tự luận.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        question.IsPublishedForPractice = !question.IsPublishedForPractice;
        await db.SaveChangesAsync(ct);
        var lopIds = await db.EssayQuestionLopVisibilities.Where(v => v.EssayQuestionId == id).Select(v => v.LopId).ToListAsync(ct);
        return ToResponse(question, lopIds);
    }

    public async Task<EssayQuestionResponse> UpdateLopVisibilityAsync(Guid id, List<Guid> lopIds, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.EssayQuestions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi tự luận.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        var distinctLopIds = lopIds.Distinct().ToList();
        var existingLopIds = await db.EssayQuestionLopVisibilities.Where(v => v.EssayQuestionId == id).Select(v => v.LopId).ToListAsync(ct);
        var touchedLopIds = distinctLopIds.Union(existingLopIds).ToList();
        await lopScopeGuard.EnsureCanAssignAsync(touchedLopIds, callerRole, ct);

        var current = await db.EssayQuestionLopVisibilities.Where(v => v.EssayQuestionId == id).ToListAsync(ct);
        db.EssayQuestionLopVisibilities.RemoveRange(current);
        foreach (var lopId in distinctLopIds)
        {
            db.EssayQuestionLopVisibilities.Add(new EssayQuestionLopVisibility { EssayQuestionId = id, LopId = lopId });
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(question, distinctLopIds);
    }

    private static void EnsureOwnerOrAdmin(EssayQuestion question, Guid callerUserId, string callerRole)
    {
        if (callerRole == Roles.Admin) return;
        if (question.CreatedBy == callerUserId) return;
        throw new UnauthorizedAccessException("Bạn chỉ được sửa/xóa câu hỏi tự luận do chính mình tạo.");
    }

    private async Task<Dictionary<Guid, List<Guid>>> LoadLopIdsAsync(List<Guid> questionIds, CancellationToken ct)
    {
        var rows = await db.EssayQuestionLopVisibilities.Where(v => questionIds.Contains(v.EssayQuestionId)).ToListAsync(ct);
        return rows.GroupBy(v => v.EssayQuestionId).ToDictionary(g => g.Key, g => g.Select(v => v.LopId).ToList());
    }

    private static EssayQuestionResponse ToResponse(EssayQuestion q, Dictionary<Guid, List<Guid>> lopIdsByQuestion) =>
        ToResponse(q, lopIdsByQuestion.TryGetValue(q.Id, out var lopIds) ? lopIds : []);

    private static EssayQuestionResponse ToResponse(EssayQuestion q, List<Guid> lopIds) => new(
        q.Id, q.Chapter, q.QuestionText, q.SuggestedAnswer, q.CreatedBy, q.CreatedAtUtc, q.SourceType, q.SourceMaterialId, q.IsPublishedForPractice, lopIds);
}
