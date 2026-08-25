using QuizService.Api.Dtos;
using QuizService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;

namespace QuizService.Api.Endpoints;

/// <summary>Việc 4.2 mục 3 (2026-08-19) — nội bộ, chỉ admin-service gọi (RequireInternalServiceKeyFilter,
/// cùng nguyên tắc StatsEndpoints/scores-by-users). Dump PHẢI được gọi và backup PHẢI tải về thành
/// công ở admin-service TRƯỚC KHI gọi /delete — thứ tự đó được admin-service's LopDeletionService
/// đảm bảo, quiz-service chỉ cung cấp 2 thao tác nguyên tử độc lập, không tự biết về thứ tự đó.</summary>
public static class LopDataAdminEndpoints
{
    public static IEndpointRouteBuilder MapLopDataAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/lop-data").WithTags("LopDataAdmin")
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapPost("/dump", async (LopDataRequest request, ILopDataAdminService service, CancellationToken ct) =>
        {
            var result = await service.DumpAsync(request, ct);
            return Results.Ok(ApiResponse<LopDataDumpResponse>.Ok(result));
        });

        group.MapPost("/delete", async (LopDataRequest request, ILopDataAdminService service, CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(request, ct);
            return Results.Ok(ApiResponse<LopDataDeleteResponse>.Ok(result));
        });

        return app;
    }
}
