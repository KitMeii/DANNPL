using AdminService.Api.Dtos;

namespace AdminService.Api.Services;

public interface ITeacherOverviewService
{
    Task<IReadOnlyList<TeacherOverviewResponse>> GetTeacherOverviewAsync(CancellationToken ct);

    Task<IReadOnlyList<ChapterQuestionCountResponse>> GetQuestionCountsByChapterAsync(CancellationToken ct);

    /// <summary>Việc D (2026-08-16) — drill-down: từng Lớp của 1 giáo viên kèm điểm TB riêng của
    /// Lớp đó (khác GetTeacherOverviewAsync, gộp mọi Lớp thành 1 số). Không cache — chỉ gọi khi
    /// Admin chủ động mở rộng 1 dòng giáo viên, tần suất thấp hơn nhiều so với bảng tổng quan.</summary>
    Task<IReadOnlyList<LopQualityResponse>> GetTeacherLopQualityAsync(Guid teacherId, CancellationToken ct);
}
