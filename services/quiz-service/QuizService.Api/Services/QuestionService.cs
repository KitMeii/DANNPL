using Microsoft.EntityFrameworkCore;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;
using Shared.Contracts;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Services;

public sealed class QuestionService(QuizDbContext db, ILopScopeGuard lopScopeGuard) : IQuestionService
{
    public async Task<IReadOnlyList<QuestionResponse>> ListAsync(string? chapter, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var query = db.Questions.AsQueryable();
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

    public async Task<IReadOnlyList<QuizQuestionResponse>> ListForPracticeAsync(string? chapter, Guid? callerLopId, CancellationToken ct)
    {
        var query = db.Questions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(chapter))
        {
            query = query.Where(q => q.Chapter == chapter);
        }

        // Việc 8: không có dòng QuestionLopVisibility nào = toàn hệ thống (hành vi cũ, không đổi).
        // Có dòng = chỉ hiện nếu 1 trong số đó khớp đúng Lớp của người gọi; học viên chưa gán Lớp
        // (callerLopId null) không khớp được bất kỳ giới hạn nào nên chỉ thấy câu toàn hệ thống.
        return await query
            .Where(q => q.IsPublishedForPractice)
            .Where(q => !db.QuestionLopVisibilities.Any(v => v.QuestionId == q.Id) ||
                        (callerLopId != null && db.QuestionLopVisibilities.Any(v => v.QuestionId == q.Id && v.LopId == callerLopId)))
            .OrderBy(q => q.CreatedAtUtc)
            .Select(q => new QuizQuestionResponse(q.Id, q.Chapter, q.QuestionText, q.OptionA, q.OptionB, q.OptionC, q.OptionD))
            .ToListAsync(ct);
    }

    public async Task<QuestionResponse> CreateAsync(CreateQuestionRequest request, Guid createdBy, string callerRole, CancellationToken ct)
    {
        var lopIds = (request.LopIds ?? []).Distinct().ToList();
        await lopScopeGuard.EnsureCanAssignAsync(lopIds, callerRole, ct);

        var question = new Question
        {
            Chapter = request.Chapter?.Trim(),
            QuestionText = request.QuestionText.Trim(),
            OptionA = request.OptionA,
            OptionB = request.OptionB,
            OptionC = request.OptionC,
            OptionD = request.OptionD,
            CorrectAnswer = request.CorrectAnswer,
            Explanation = request.Explanation?.Trim(),
            CreatedBy = createdBy,
            SourceType = request.SourceType,
            SourceMaterialId = request.SourceMaterialId,
            Difficulty = request.Difficulty,
            Topic = request.Topic?.Trim(),
            // Manual (giáo viên tự soạn) coi như đã xuất bản — giữ nguyên trải nghiệm cũ.
            // AiGenerated/Imported cần giáo viên chủ động xuất bản sau khi kiểm duyệt (C3).
            IsPublishedForPractice = request.SourceType == "Manual",
        };

        db.Questions.Add(question);
        foreach (var lopId in lopIds)
        {
            db.QuestionLopVisibilities.Add(new QuestionLopVisibility { QuestionId = question.Id, LopId = lopId });
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(question, lopIds);
    }

    public async Task<QuestionResponse> UpdateAsync(Guid id, UpdateQuestionRequest request, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        question.Chapter = request.Chapter?.Trim();
        question.QuestionText = request.QuestionText.Trim();
        question.OptionA = request.OptionA;
        question.OptionB = request.OptionB;
        question.OptionC = request.OptionC;
        question.OptionD = request.OptionD;
        question.CorrectAnswer = request.CorrectAnswer;
        question.Explanation = request.Explanation?.Trim();

        await db.SaveChangesAsync(ct);
        var lopIds = await db.QuestionLopVisibilities.Where(v => v.QuestionId == id).Select(v => v.LopId).ToListAsync(ct);
        return ToResponse(question, lopIds);
    }

    public async Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        // Audit "Việc 1": ExamVersionQuestion.QuestionId là Cascade ở tầng DB — nếu không chặn ở
        // đây, xóa câu hỏi này sẽ âm thầm làm mã đề ĐÃ XUẤT BẢN co lại (mất 1 câu, không cảnh báo
        // gì), đã xác nhận qua test thật. Mã đề CHƯA xuất bản vẫn cho xóa tự do (cascade bình
        // thường), chỉ chặn khi ít nhất 1 ExamVersion chứa câu này đang IsPublished=true.
        var publishedMaDe = await db.ExamVersionQuestions
            .Where(evq => evq.QuestionId == id)
            .Join(db.ExamVersions.Where(v => v.IsPublished), evq => evq.ExamVersionId, v => v.Id, (evq, v) => v.MaDe)
            .ToListAsync(ct);
        if (publishedMaDe.Count > 0)
        {
            var maDeList = string.Join(", ", publishedMaDe.Select(m => $"\"{m}\""));
            throw new ConflictException(
                $"Không thể xóa câu hỏi đang thuộc mã đề {maDeList} đã xuất bản cho học viên — hủy xuất bản mã đề trước nếu muốn xóa.");
        }

        db.Questions.Remove(question);
        await db.SaveChangesAsync(ct);
    }

    public async Task<QuestionResponse> TogglePublishAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        question.IsPublishedForPractice = !question.IsPublishedForPractice;
        await db.SaveChangesAsync(ct);
        var lopIds = await db.QuestionLopVisibilities.Where(v => v.QuestionId == id).Select(v => v.LopId).ToListAsync(ct);
        return ToResponse(question, lopIds);
    }

    public async Task<QuestionResponse> UpdateLopVisibilityAsync(Guid id, List<Guid> lopIds, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy câu hỏi.");
        EnsureOwnerOrAdmin(question, callerUserId, callerRole);

        var distinctLopIds = lopIds.Distinct().ToList();
        // Cả 2 chiều đều cần kiểm ownership: gán MỚI tới Lớp không phải mình, VÀ gỡ giới hạn khỏi
        // Lớp không phải mình (đưa về toàn hệ thống) đều là hành động trên phạm vi của Lớp đó.
        var existingLopIds = await db.QuestionLopVisibilities.Where(v => v.QuestionId == id).Select(v => v.LopId).ToListAsync(ct);
        var touchedLopIds = distinctLopIds.Union(existingLopIds).ToList();
        await lopScopeGuard.EnsureCanAssignAsync(touchedLopIds, callerRole, ct);

        var current = await db.QuestionLopVisibilities.Where(v => v.QuestionId == id).ToListAsync(ct);
        db.QuestionLopVisibilities.RemoveRange(current);
        foreach (var lopId in distinctLopIds)
        {
            db.QuestionLopVisibilities.Add(new QuestionLopVisibility { QuestionId = id, LopId = lopId });
        }

        await db.SaveChangesAsync(ct);
        return ToResponse(question, distinctLopIds);
    }

    private async Task<Dictionary<Guid, List<Guid>>> LoadLopIdsAsync(List<Guid> questionIds, CancellationToken ct)
    {
        var rows = await db.QuestionLopVisibilities.Where(v => questionIds.Contains(v.QuestionId)).ToListAsync(ct);
        return rows.GroupBy(v => v.QuestionId).ToDictionary(g => g.Key, g => g.Select(v => v.LopId).ToList());
    }

    private static QuestionResponse ToResponse(Question q, Dictionary<Guid, List<Guid>> lopIdsByQuestion) =>
        ToResponse(q, lopIdsByQuestion.TryGetValue(q.Id, out var lopIds) ? lopIds : []);

    private static QuestionResponse ToResponse(Question q, List<Guid> lopIds) => new(
        q.Id, q.Chapter, q.QuestionText, q.OptionA, q.OptionB, q.OptionC, q.OptionD,
        q.CorrectAnswer, q.Explanation, q.CreatedBy, q.CreatedAtUtc, q.SourceType, q.SourceMaterialId,
        q.Difficulty, q.Topic, q.IsPublishedForPractice, lopIds);

    /// <summary>Rà soát Lần VIII (2026-08-21) — chỉ người tạo (CreatedBy) hoặc Admin được sửa/xóa/
    /// xuất bản/đổi phạm vi Lớp của 1 câu hỏi. CreatedBy null (câu tạo trước khi field này tồn tại,
    /// hiện DB không còn dòng nào null nhưng giữ phòng hờ) coi như KHÔNG có chủ — chỉ Admin sửa
    /// được, tránh 1 GV bất kỳ nhận vơ quyền sở hữu câu hỏi không rõ nguồn gốc.</summary>
    private static void EnsureOwnerOrAdmin(Question question, Guid callerUserId, string callerRole)
    {
        if (callerRole == Roles.Admin) return;
        if (question.CreatedBy == callerUserId) return;
        throw new UnauthorizedAccessException("Bạn chỉ được sửa/xóa câu hỏi do chính mình tạo.");
    }
}
