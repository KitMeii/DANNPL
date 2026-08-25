namespace AdminService.Api.Clients;

public sealed record RemoteStudentProgress(Guid UserId, int Streak, DateOnly? LastStudyDate, int TotalStudyMinutes, int TotalAttempts, decimal ScoreSum, DateTime UpdatedAtUtc);
public sealed record RemoteStudyLog(Guid Id, Guid UserId, DateOnly StudyDate, int Minutes, DateTime CreatedAtUtc);

public sealed record RemoteProgressLopDataDump(IReadOnlyList<RemoteStudentProgress> StudentProgress, IReadOnlyList<RemoteStudyLog> StudyLogs);

public sealed record RemoteProgressLopDataDeleteResult(int StudentProgressDeleted, int StudyLogsDeleted);

/// <summary>Việc 4.2 mục 3 (2026-08-19) — dump/xóa dữ liệu progress-service của 1 nhóm UserId (học
/// viên 1 Lớp). Gọi tới progress-service's /internal/lop-data/* (RequireInternalServiceKeyFilter).
/// Client MỚI hoàn toàn — trước Việc 4.2 admin-service chưa từng gọi sang progress-service.</summary>
public interface IProgressLopDataClient
{
    Task<RemoteProgressLopDataDump> DumpAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);
    Task<RemoteProgressLopDataDeleteResult> DeleteAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);
}
