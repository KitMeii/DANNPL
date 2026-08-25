using Microsoft.EntityFrameworkCore;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;
using Shared.Contracts;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Services;

public sealed class PracticeSetService(QuizDbContext db, ILopScopeGuard lopScopeGuard, IQuestionService questionService) : IPracticeSetService
{
    public async Task<IReadOnlyList<ChapterOptionResponse>> ListChapterOptionsAsync(CancellationToken ct)
    {
        var rows = await db.Questions
            .Where(q => q.IsPublishedForPractice && q.Chapter != null)
            .GroupBy(q => q.Chapter!)
            .Select(g => new { Chapter = g.Key, Count = g.Count() })
            .OrderBy(c => c.Chapter)
            .ToListAsync(ct);

        return rows.Select(r => new ChapterOptionResponse(r.Chapter, r.Count)).ToList();
    }

    public async Task<PracticeSetResponse> CreateAsync(CreatePracticeSetRequest request, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var lopIds = request.LopIds.Distinct().ToList();
        await lopScopeGuard.EnsureCanAssignAsync(lopIds, callerRole, ct);

        var practiceSet = new PracticeSet
        {
            Ten = request.Ten.Trim(),
            Chapter = request.Chapter.Trim(),
            GiaoVienId = callerUserId,
        };

        db.PracticeSets.Add(practiceSet);
        foreach (var lopId in lopIds)
        {
            db.PracticeSetLopVisibilities.Add(new PracticeSetLopVisibility { PracticeSetId = practiceSet.Id, LopId = lopId });
        }

        await db.SaveChangesAsync(ct);

        var questionCount = await db.Questions.CountAsync(q => q.Chapter == practiceSet.Chapter && q.IsPublishedForPractice, ct);
        return ToResponse(practiceSet, lopIds, questionCount);
    }

    public async Task<IReadOnlyList<PracticeSetResponse>> ListMineAsync(Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var query = db.PracticeSets.AsQueryable();
        if (callerRole != Roles.Admin)
        {
            query = query.Where(p => p.GiaoVienId == callerUserId);
        }

        var sets = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        var lopIdsBySet = await LoadLopIdsAsync(sets.Select(s => s.Id).ToList(), ct);

        // Đếm theo góc nhìn ngân hàng chung (không lọc Lớp cụ thể) — giáo viên xem đề mình tạo cần
        // biết tổng số câu đã publish trong chương đó, không phải số 1 học viên cụ thể sẽ thấy.
        var chapterCounts = await db.Questions
            .Where(q => q.IsPublishedForPractice)
            .GroupBy(q => q.Chapter)
            .Select(g => new { Chapter = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Chapter ?? "", x => x.Count, ct);

        return sets.Select(s => ToResponse(s, lopIdsBySet.GetValueOrDefault(s.Id, []), chapterCounts.GetValueOrDefault(s.Chapter, 0))).ToList();
    }

    public async Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct)
    {
        var practiceSet = await db.PracticeSets.FindAsync([id], ct)
            ?? throw new NotFoundException("Không tìm thấy đề luyện tập.");

        if (callerRole != Roles.Admin && practiceSet.GiaoVienId != callerUserId)
        {
            throw new UnauthorizedAccessException("Chỉ giáo viên đã tạo đề này (hoặc Admin) mới được xóa.");
        }

        db.PracticeSets.Remove(practiceSet);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PracticeSetResponse>> ListAvailableAsync(Guid? callerLopId, CancellationToken ct)
    {
        // Việc 4.4 Phần B: 0 dòng visibility = toàn hệ thống (quy ước kỹ thuật giữ nhất quán với 4
        // bảng kia), có dòng = chỉ khớp đúng callerLopId — cùng công thức QuestionService.
        // ListForPracticeAsync. Thực tế UI luôn bắt buộc ≥1 Lớp khi tạo nên nhánh "toàn hệ thống"
        // hiếm khi phát sinh, nhưng công thức vẫn đúng nếu có (vd dữ liệu import tay).
        var sets = await db.PracticeSets
            .Where(p => !db.PracticeSetLopVisibilities.Any(v => v.PracticeSetId == p.Id) ||
                        (callerLopId != null && db.PracticeSetLopVisibilities.Any(v => v.PracticeSetId == p.Id && v.LopId == callerLopId)))
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(ct);

        var lopIdsBySet = await LoadLopIdsAsync(sets.Select(s => s.Id).ToList(), ct);

        var result = new List<PracticeSetResponse>();
        foreach (var s in sets)
        {
            // "Đề sống" — đếm lại MỖI LẦN gọi, đúng những câu callerLopId này sẽ thấy nếu vào làm
            // ngay bây giờ (câu mới publish thêm vào chương tự động phản ánh, không cần thao tác gì).
            var questionCount = (await questionService.ListForPracticeAsync(s.Chapter, callerLopId, ct)).Count;
            result.Add(ToResponse(s, lopIdsBySet.GetValueOrDefault(s.Id, []), questionCount));
        }

        return result;
    }

    private async Task<Dictionary<Guid, List<Guid>>> LoadLopIdsAsync(List<Guid> practiceSetIds, CancellationToken ct)
    {
        var rows = await db.PracticeSetLopVisibilities.Where(v => practiceSetIds.Contains(v.PracticeSetId)).ToListAsync(ct);
        return rows.GroupBy(v => v.PracticeSetId).ToDictionary(g => g.Key, g => g.Select(v => v.LopId).ToList());
    }

    private static PracticeSetResponse ToResponse(PracticeSet p, List<Guid> lopIds, int questionCount) =>
        new(p.Id, p.Ten, p.Chapter, p.GiaoVienId, p.CreatedAtUtc, lopIds, questionCount);
}
