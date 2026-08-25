using AdminService.Api.Dtos;

namespace AdminService.Api.Services;

public interface ISystemOverviewService
{
    Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken ct);
}
