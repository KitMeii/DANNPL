using ProgressService.Api.Dtos;
using ProgressService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;

namespace ProgressService.Api.Endpoints;

/// <summary>Việc 4.2 mục 3 (2026-08-19) — nội bộ, chỉ admin-service gọi. Song song với
/// quiz-service's LopDataAdminEndpoints, xem remarks ở đó.</summary>
public static class LopDataAdminEndpoints
{
    public static IEndpointRouteBuilder MapLopDataAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/lop-data").WithTags("LopDataAdmin")
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapPost("/dump", async (ProgressLopDataRequest request, ILopDataAdminService service, CancellationToken ct) =>
        {
            var result = await service.DumpAsync(request, ct);
            return Results.Ok(ApiResponse<ProgressLopDataDumpResponse>.Ok(result));
        });

        group.MapPost("/delete", async (ProgressLopDataRequest request, ILopDataAdminService service, CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(request, ct);
            return Results.Ok(ApiResponse<ProgressLopDataDeleteResponse>.Ok(result));
        });

        return app;
    }
}
