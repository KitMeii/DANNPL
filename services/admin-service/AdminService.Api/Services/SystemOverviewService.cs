using AdminService.Api.Clients;
using AdminService.Api.Dtos;
using Shared.Contracts;

namespace AdminService.Api.Services;

public sealed class SystemOverviewService(IAuthAdminClient authClient, ISystemStatsClient statsClient) : ISystemOverviewService
{
    public async Task<SystemOverviewResponse> GetOverviewAsync(CancellationToken ct)
    {
        var users = await authClient.ListUsersAsync(null, null, null, ct);
        var overview = await statsClient.GetOverviewAsync(ct);

        return new SystemOverviewResponse(
            users.Count(u => u.Role == Roles.Student),
            users.Count(u => u.Role == Roles.Teacher),
            users.Count(u => u.Role == Roles.Admin),
            overview.MaterialCount,
            overview.QuestionCount,
            overview.OralQuestionCount);
    }
}
