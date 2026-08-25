using AdminService.Api.Data;
using AdminService.Api.Dtos;
using AdminService.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Api.Services;

public sealed class SystemConfigService(AdminDbContext db) : ISystemConfigService
{
    public async Task<IReadOnlyList<SystemConfigResponse>> GetAllAsync(CancellationToken ct)
    {
        return await db.SystemConfigs
            .OrderBy(c => c.Key)
            .Select(c => new SystemConfigResponse(c.Key, c.Value, c.UpdatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<SystemConfigResponse> SetAsync(string key, string value, Guid updatedBy, CancellationToken ct)
    {
        var config = await db.SystemConfigs.FindAsync([key], ct);
        if (config is null)
        {
            config = new SystemConfig { Key = key, Value = value, UpdatedBy = updatedBy };
            db.SystemConfigs.Add(config);
        }
        else
        {
            config.Value = value;
            config.UpdatedBy = updatedBy;
            config.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return new SystemConfigResponse(config.Key, config.Value, config.UpdatedAtUtc);
    }
}
