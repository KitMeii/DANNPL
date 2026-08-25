using AdminService.Api.Dtos;

namespace AdminService.Api.Services;

public interface ILopKhoaStatsService
{
    /// <summary>callerUserId/callerRole = người gọi API — Admin luôn được, Teacher chỉ được nếu
    /// đúng là GiaoVienId của lopId đó (Gap 2). Data-dependent, không thể khai báo tĩnh bằng
    /// [Authorize(Roles=)], cùng pattern với auth-service's ChangeChucVuAsync/ListHocVienAsync.</summary>
    Task<LopDiemTrungBinhResponse> GetLopStatsAsync(Guid lopId, Guid callerUserId, string callerRole, CancellationToken ct);
    Task<KhoaDiemTrungBinhResponse> GetKhoaStatsAsync(Guid khoaId, CancellationToken ct);
}
