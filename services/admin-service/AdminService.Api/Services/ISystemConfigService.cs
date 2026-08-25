using AdminService.Api.Dtos;

namespace AdminService.Api.Services;

public interface ISystemConfigService
{
    Task<IReadOnlyList<SystemConfigResponse>> GetAllAsync(CancellationToken ct);
    Task<SystemConfigResponse> SetAsync(string key, string value, Guid updatedBy, CancellationToken ct);
}
