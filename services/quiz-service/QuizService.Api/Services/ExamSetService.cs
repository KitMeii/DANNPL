using Microsoft.EntityFrameworkCore;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;
using Shared.Contracts;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Services;

/// <summary>C2 — trích 1 tập con cân đối độ khó (vd 50 câu) từ 1 pool lớn (vd 150 câu) và sinh N
/// "mã đề" (ExamVersion) khác nhau, mỗi mã đề chạy lại thuật toán chọn với 1 seed khác nhau.
/// Không tự publish cho học viên luyện tập — đó là hành động riêng (C3, PUT .../publish).</summary>
public sealed class ExamSetService(QuizDbContext db, ILopScopeGuard lopScopeGuard) : IExamSetService
{
    private const int BaseMaDe = 101;

    public async Task<ExamSetResponse> GenerateAsync(GenerateExamSetVersionsRequest request, Guid createdBy, string callerRole, CancellationToken ct)
    {
        var lopIds = (request.LopIds ?? []).Distinct().ToList();
        await lopScopeGuard.EnsureCanAssignAsync(lopIds, callerRole, ct);

        var pool = await db.Questions.Where(q => request.PoolQuestionIds.Contains(q.Id)).ToListAsync(ct);
        if (pool.Count == 0)
        {
            throw new FluentValidation.ValidationException("Không tìm thấy câu hỏi nào trong pool đã chọn.");
        }
        if (request.TargetCount > pool.Count)
        {
            throw new FluentValidation.ValidationException($"Số câu muốn lấy ({request.TargetCount}) vượt quá số câu trong pool ({pool.Count}).");
        }

        var examSet = new ExamSet
        {
            Ten = request.Ten.Trim(),
            MaterialId = request.MaterialId,
            TotalPoolSize = pool.Count,
            CreatedBy = createdBy,
        };
        db.ExamSets.Add(examSet);

        var previousSelections = new List<HashSet<Guid>>();
        var versions = new List<(ExamVersion Version, List<Question> Questions)>();

        for (var v = 0; v < request.VersionCount; v++)
        {
            var baseSeed = HashCode.Combine(examSet.Id, v);
            List<Question> selected;
            HashSet<Guid> selectedIds;
            var attempt = 0;
            do
            {
                var rng = new Random(baseSeed + attempt);
                selected = BalancedSelection.Select(pool, request.TargetCount, rng);
                selectedIds = selected.Select(q => q.Id).ToHashSet();
                attempt++;
                // An toàn: dừng thử lại sau 20 lần dù vẫn trùng (pool quá nhỏ so với TargetCount
                // có thể khiến các tập con luôn giống hệt nhau — không phải lỗi, chỉ là giới hạn
                // toán học khi pool gần bằng TargetCount).
            } while (previousSelections.Any(prev => prev.SetEquals(selectedIds)) && attempt < 20);

            previousSelections.Add(selectedIds);
            var version = new ExamVersion { ExamSetId = examSet.Id, MaDe = (BaseMaDe + v).ToString(), Kind = "Mcq" };
            versions.Add((version, selected));
        }

        db.ExamVersions.AddRange(versions.Select(v => v.Version));
        foreach (var (version, questions) in versions)
        {
            for (var i = 0; i < questions.Count; i++)
            {
                db.ExamVersionQuestions.Add(new ExamVersionQuestion { ExamVersionId = version.Id, QuestionId = questions[i].Id, ThuTu = i + 1 });
            }
            foreach (var lopId in lopIds)
            {
                db.ExamVersionLopVisibilities.Add(new ExamVersionLopVisibility { ExamVersionId = version.Id, LopId = lopId });
            }
        }

        await db.SaveChangesAsync(ct);

        return new ExamSetResponse(
            examSet.Id, examSet.Ten, examSet.MaterialId, examSet.TotalPoolSize, examSet.CreatedBy, examSet.CreatedAtUtc,
            versions.Select(v => new ExamVersionResponse(v.Version.Id, v.Version.MaDe, v.Version.IsPublished, v.Version.Kind, v.Questions.Select(q => ToQuestionResponse(q, [])).ToList(), [], lopIds)).ToList());
    }

    /// <summary>Việc 5 — "Bộ đề VĐ mới" từ ngân hàng câu hỏi vấn đáp có sẵn, không sinh AI mới.
    /// Chọn ngẫu nhiên đơn giản (không BalancedSelection): OralQuestion không có Topic, và pool/
    /// targetCount ở quy mô nhỏ (1-4 câu) nên thuật toán cân đối độ khó phức tạp không cần thiết —
    /// khớp đúng đánh giá "random đơn giản" đã duyệt cho trường hợp này.</summary>
    public async Task<ExamSetResponse> GenerateOralAsync(GenerateOralExamSetVersionsRequest request, Guid createdBy, string callerRole, CancellationToken ct)
    {
        var lopIds = (request.LopIds ?? []).Distinct().ToList();
        await lopScopeGuard.EnsureCanAssignAsync(lopIds, callerRole, ct);

        var pool = await db.OralQuestions.Where(q => request.PoolOralQuestionIds.Contains(q.Id)).ToListAsync(ct);
        if (pool.Count == 0)
        {
            throw new FluentValidation.ValidationException("Không tìm thấy câu hỏi nào trong pool đã chọn.");
        }
        if (request.TargetCount > pool.Count)
        {
            throw new FluentValidation.ValidationException($"Số câu muốn lấy ({request.TargetCount}) vượt quá số câu trong pool ({pool.Count}).");
        }

        var examSet = new ExamSet
        {
            Ten = request.Ten.Trim(),
            TotalPoolSize = pool.Count,
            CreatedBy = createdBy,
        };
        db.ExamSets.Add(examSet);

        var previousSelections = new List<HashSet<Guid>>();
        var versions = new List<(ExamVersion Version, List<OralQuestion> Questions)>();

        for (var v = 0; v < request.VersionCount; v++)
        {
            var baseSeed = HashCode.Combine(examSet.Id, v);
            List<OralQuestion> selected;
            HashSet<Guid> selectedIds;
            var attempt = 0;
            do
            {
                var rng = new Random(baseSeed + attempt);
                selected = pool.OrderBy(_ => rng.Next()).Take(request.TargetCount).ToList();
                selectedIds = selected.Select(q => q.Id).ToHashSet();
                attempt++;
            } while (previousSelections.Any(prev => prev.SetEquals(selectedIds)) && attempt < 20);

            previousSelections.Add(selectedIds);
            var version = new ExamVersion { ExamSetId = examSet.Id, MaDe = (BaseMaDe + v).ToString(), Kind = "Oral" };
            versions.Add((version, selected));
        }

        db.ExamVersions.AddRange(versions.Select(v => v.Version));
        foreach (var (version, questions) in versions)
        {
            for (var i = 0; i < questions.Count; i++)
            {
                db.ExamVersionOralQuestions.Add(new ExamVersionOralQuestion { ExamVersionId = version.Id, OralQuestionId = questions[i].Id, ThuTu = i + 1 });
            }
            foreach (var lopId in lopIds)
            {
                db.ExamVersionLopVisibilities.Add(new ExamVersionLopVisibility { ExamVersionId = version.Id, LopId = lopId });
            }
        }

        await db.SaveChangesAsync(ct);

        var allOralIds = versions.SelectMany(v => v.Questions.Select(q => q.Id)).Distinct().ToList();
        var oralLopIdRows = await db.OralQuestionLopVisibilities.Where(v => allOralIds.Contains(v.OralQuestionId)).ToListAsync(ct);
        var oralLopIdsByQuestion = oralLopIdRows.GroupBy(v => v.OralQuestionId).ToDictionary(g => g.Key, g => g.Select(v => v.LopId).ToList());

        return new ExamSetResponse(
            examSet.Id, examSet.Ten, examSet.MaterialId, examSet.TotalPoolSize, examSet.CreatedBy, examSet.CreatedAtUtc,
            versions.Select(v => new ExamVersionResponse(v.Version.Id, v.Version.MaDe, v.Version.IsPublished, v.Version.Kind, [],
                v.Questions.Select(q => ToOralQuestionResponse(q, oralLopIdsByQuestion.TryGetValue(q.Id, out var oLopIds) ? oLopIds : [])).ToList(), lopIds)).ToList());
    }

    public async Task<IReadOnlyList<ExamSetSummaryResponse>> ListAsync(Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var query = db.ExamSets.AsQueryable();
        if (callerRole != Roles.Admin)
        {
            query = query.Where(e => e.CreatedBy == callerUserId);
        }

        return await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new ExamSetSummaryResponse(e.Id, e.Ten, e.TotalPoolSize, db.ExamVersions.Count(v => v.ExamSetId == e.Id), e.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<ExamSetResponse> GetByIdAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var examSet = await db.ExamSets.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy bộ đề.");
        EnsureOwnerOrAdmin(examSet, callerUserId, callerRole);

        var versions = await db.ExamVersions.Where(v => v.ExamSetId == id).OrderBy(v => v.MaDe).ToListAsync(ct);
        var versionResponses = new List<ExamVersionResponse>();
        foreach (var version in versions)
        {
            var versionLopIds = await db.ExamVersionLopVisibilities.Where(v => v.ExamVersionId == version.Id).Select(v => v.LopId).ToListAsync(ct);

            if (version.Kind == "Oral")
            {
                var oralIds = await db.ExamVersionOralQuestions
                    .Where(evq => evq.ExamVersionId == version.Id)
                    .OrderBy(evq => evq.ThuTu)
                    .Select(evq => evq.OralQuestionId)
                    .ToListAsync(ct);
                var oralQuestions = await db.OralQuestions.Where(q => oralIds.Contains(q.Id)).ToListAsync(ct);
                var orderedOral = oralIds.Select(oid => oralQuestions.First(q => q.Id == oid)).ToList();
                var oralLopIdRows = await db.OralQuestionLopVisibilities.Where(v => oralIds.Contains(v.OralQuestionId)).ToListAsync(ct);
                var oralLopIdsByQuestion = oralLopIdRows.GroupBy(v => v.OralQuestionId).ToDictionary(g => g.Key, g => g.Select(v => v.LopId).ToList());
                versionResponses.Add(new ExamVersionResponse(version.Id, version.MaDe, version.IsPublished, version.Kind, [],
                    orderedOral.Select(q => ToOralQuestionResponse(q, oralLopIdsByQuestion.TryGetValue(q.Id, out var oLopIds) ? oLopIds : [])).ToList(), versionLopIds));
                continue;
            }

            var questionIds = await db.ExamVersionQuestions
                .Where(evq => evq.ExamVersionId == version.Id)
                .OrderBy(evq => evq.ThuTu)
                .Select(evq => evq.QuestionId)
                .ToListAsync(ct);
            var questions = await db.Questions.Where(q => questionIds.Contains(q.Id)).ToListAsync(ct);
            var ordered = questionIds.Select(qid => questions.First(q => q.Id == qid)).ToList();
            var questionLopIds = await db.QuestionLopVisibilities.Where(v => questionIds.Contains(v.QuestionId)).ToListAsync(ct);
            var questionLopIdsByQuestion = questionLopIds.GroupBy(v => v.QuestionId).ToDictionary(g => g.Key, g => g.Select(v => v.LopId).ToList());
            versionResponses.Add(new ExamVersionResponse(version.Id, version.MaDe, version.IsPublished, version.Kind, ordered.Select(q => ToQuestionResponse(q, questionLopIdsByQuestion.TryGetValue(q.Id, out var lopIds) ? lopIds : [])).ToList(), [], versionLopIds));
        }

        return new ExamSetResponse(examSet.Id, examSet.Ten, examSet.MaterialId, examSet.TotalPoolSize, examSet.CreatedBy, examSet.CreatedAtUtc, versionResponses);
    }

    public async Task<int> PublishVersionAsync(Guid versionId, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var version = await db.ExamVersions.FindAsync([versionId], ct)
            ?? throw new NotFoundException("Không tìm thấy mã đề.");
        await EnsureVersionOwnerOrAdminAsync(version, callerUserId, callerRole, ct);
        if (version.Kind != "Mcq")
        {
            // OralQuestion không có publish-gate (audit trước xác nhận cố ý) — mã đề Kind=Oral
            // không có gì để publish, chặn tường minh thay vì no-op âm thầm.
            throw new FluentValidation.ValidationException("Mã đề vấn đáp không có bước xuất bản — chỉ dùng để xuất Word.");
        }

        var questionIds = await db.ExamVersionQuestions
            .Where(evq => evq.ExamVersionId == versionId)
            .Select(evq => evq.QuestionId)
            .ToListAsync(ct);

        var questions = await db.Questions.Where(q => questionIds.Contains(q.Id)).ToListAsync(ct);
        foreach (var question in questions)
        {
            question.IsPublishedForPractice = true;
        }
        version.IsPublished = true;

        // Việc 8: mã đề có giới hạn Lớp → gán đúng phạm vi đó cho các câu hỏi thành viên ĐANG toàn
        // hệ thống (chưa có dòng QuestionLopVisibility nào). Câu hỏi đã có phạm vi riêng (giáo viên
        // tự gán trước đó, độc lập với mã đề này) được giữ nguyên — xem ExamVersionLopVisibility.cs.
        var versionLopIds = await db.ExamVersionLopVisibilities.Where(v => v.ExamVersionId == versionId).Select(v => v.LopId).ToListAsync(ct);
        if (versionLopIds.Count > 0)
        {
            var alreadyScopedQuestionIds = (await db.QuestionLopVisibilities
                .Where(v => questionIds.Contains(v.QuestionId))
                .Select(v => v.QuestionId)
                .ToListAsync(ct)).ToHashSet();

            foreach (var questionId in questionIds.Where(qid => !alreadyScopedQuestionIds.Contains(qid)))
            {
                foreach (var lopId in versionLopIds)
                {
                    db.QuestionLopVisibilities.Add(new QuestionLopVisibility { QuestionId = questionId, LopId = lopId });
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return questions.Count;
    }

    /// <summary>Thêm sau audit "Việc 1" — cần tồn tại để thông điệp lỗi 409 khi chặn xóa câu hỏi
    /// ("hủy xuất bản mã đề trước nếu muốn xóa") thực sự có lối thoát khả thi cho giáo viên.
    /// Với mỗi câu trong mã đề: chỉ đặt lại IsPublishedForPractice=false nếu câu đó KHÔNG còn
    /// thuộc bất kỳ ExamVersion nào khác đang IsPublished=true (tránh gỡ nhầm 1 câu vẫn đang hợp lệ
    /// hiển thị cho học viên qua mã đề khác — đã xảy ra thật do cho phép trùng lặp câu giữa các
    /// mã, xem BalancedSelection/test "2 mã đề KHÔNG giống hệt nhau nhưng CÓ THỂ trùng 1 phần").</summary>
    public async Task<int> UnpublishVersionAsync(Guid versionId, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var version = await db.ExamVersions.FindAsync([versionId], ct)
            ?? throw new NotFoundException("Không tìm thấy mã đề.");
        await EnsureVersionOwnerOrAdminAsync(version, callerUserId, callerRole, ct);
        if (version.Kind != "Mcq")
        {
            throw new FluentValidation.ValidationException("Mã đề vấn đáp không có bước xuất bản — chỉ dùng để xuất Word.");
        }

        var questionIds = await db.ExamVersionQuestions
            .Where(evq => evq.ExamVersionId == versionId)
            .Select(evq => evq.QuestionId)
            .ToListAsync(ct);

        version.IsPublished = false;
        await db.SaveChangesAsync(ct);

        var stillPublishedElsewhere = await db.ExamVersionQuestions
            .Where(evq => questionIds.Contains(evq.QuestionId) && evq.ExamVersionId != versionId)
            .Join(db.ExamVersions.Where(v => v.IsPublished), evq => evq.ExamVersionId, v => v.Id, (evq, v) => evq.QuestionId)
            .Distinct()
            .ToListAsync(ct);
        var stillPublishedSet = stillPublishedElsewhere.ToHashSet();

        var questionsToUnpublish = await db.Questions
            .Where(q => questionIds.Contains(q.Id) && !stillPublishedSet.Contains(q.Id))
            .ToListAsync(ct);
        foreach (var question in questionsToUnpublish)
        {
            question.IsPublishedForPractice = false;
        }

        await db.SaveChangesAsync(ct);
        return questionsToUnpublish.Count;
    }

    public async Task<ExamVersionResponse> UpdateVersionLopVisibilityAsync(Guid versionId, List<Guid> lopIds, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var version = await db.ExamVersions.FindAsync([versionId], ct)
            ?? throw new NotFoundException("Không tìm thấy mã đề.");
        await EnsureVersionOwnerOrAdminAsync(version, callerUserId, callerRole, ct);

        var distinctLopIds = lopIds.Distinct().ToList();
        var existingLopIds = await db.ExamVersionLopVisibilities.Where(v => v.ExamVersionId == versionId).Select(v => v.LopId).ToListAsync(ct);
        var touchedLopIds = distinctLopIds.Union(existingLopIds).ToList();
        await lopScopeGuard.EnsureCanAssignAsync(touchedLopIds, callerRole, ct);

        var current = await db.ExamVersionLopVisibilities.Where(v => v.ExamVersionId == versionId).ToListAsync(ct);
        db.ExamVersionLopVisibilities.RemoveRange(current);
        foreach (var lopId in distinctLopIds)
        {
            db.ExamVersionLopVisibilities.Add(new ExamVersionLopVisibility { ExamVersionId = versionId, LopId = lopId });
        }

        await db.SaveChangesAsync(ct);

        if (version.Kind == "Oral")
        {
            var oralIds = await db.ExamVersionOralQuestions.Where(evq => evq.ExamVersionId == versionId).OrderBy(evq => evq.ThuTu).Select(evq => evq.OralQuestionId).ToListAsync(ct);
            var oralQuestions = await db.OralQuestions.Where(q => oralIds.Contains(q.Id)).ToListAsync(ct);
            var orderedOral = oralIds.Select(oid => oralQuestions.First(q => q.Id == oid)).ToList();
            var oralLopIds = await db.OralQuestionLopVisibilities.Where(v => oralIds.Contains(v.OralQuestionId)).ToListAsync(ct);
            var oralLopIdsByQuestion = oralLopIds.GroupBy(v => v.OralQuestionId).ToDictionary(g => g.Key, g => g.Select(v => v.LopId).ToList());
            return new ExamVersionResponse(version.Id, version.MaDe, version.IsPublished, version.Kind, [],
                orderedOral.Select(q => ToOralQuestionResponse(q, oralLopIdsByQuestion.TryGetValue(q.Id, out var oLopIds) ? oLopIds : [])).ToList(), distinctLopIds);
        }

        var questionIds = await db.ExamVersionQuestions.Where(evq => evq.ExamVersionId == versionId).OrderBy(evq => evq.ThuTu).Select(evq => evq.QuestionId).ToListAsync(ct);
        var questions = await db.Questions.Where(q => questionIds.Contains(q.Id)).ToListAsync(ct);
        var ordered = questionIds.Select(qid => questions.First(q => q.Id == qid)).ToList();
        var questionLopIds = await db.QuestionLopVisibilities.Where(v => questionIds.Contains(v.QuestionId)).ToListAsync(ct);
        var questionLopIdsByQuestion = questionLopIds.GroupBy(v => v.QuestionId).ToDictionary(g => g.Key, g => g.Select(v => v.LopId).ToList());
        return new ExamVersionResponse(version.Id, version.MaDe, version.IsPublished, version.Kind, ordered.Select(q => ToQuestionResponse(q, questionLopIdsByQuestion.TryGetValue(q.Id, out var qLopIds) ? qLopIds : [])).ToList(), [], distinctLopIds);
    }

    // Rà soát Lần XI (2026-08-21) — cùng pattern EnsureOwnerOrAdmin ở QuestionService/OralQuestionService.
    private static void EnsureOwnerOrAdmin(ExamSet examSet, Guid callerUserId, string callerRole)
    {
        if (callerRole == Roles.Admin) return;
        if (examSet.CreatedBy == callerUserId) return;
        throw new UnauthorizedAccessException("Bạn chỉ được thao tác trên bộ đề do chính mình tạo.");
    }

    /// <summary>ExamVersion không tự lưu CreatedBy (chỉ ExamSet có) — tra ngược qua ExamSetId rồi
    /// kiểm ownership của ExamSet cha.</summary>
    private async Task EnsureVersionOwnerOrAdminAsync(ExamVersion version, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        if (callerRole == Roles.Admin) return;
        var examSet = await db.ExamSets.FindAsync([version.ExamSetId], ct)
            ?? throw new NotFoundException("Không tìm thấy bộ đề.");
        EnsureOwnerOrAdmin(examSet, callerUserId, callerRole);
    }

    private static QuestionResponse ToQuestionResponse(Question q, List<Guid> lopIds) => new(
        q.Id, q.Chapter, q.QuestionText, q.OptionA, q.OptionB, q.OptionC, q.OptionD,
        q.CorrectAnswer, q.Explanation, q.CreatedBy, q.CreatedAtUtc, q.SourceType, q.SourceMaterialId,
        q.Difficulty, q.Topic, q.IsPublishedForPractice, lopIds);

    private static OralQuestionResponse ToOralQuestionResponse(OralQuestion q, List<Guid> lopIds) => new(
        q.Id, q.Chapter, q.QuestionText, q.ExpectedAnswer, q.Difficulty, q.CreatedBy, q.CreatedAtUtc, lopIds);
}
