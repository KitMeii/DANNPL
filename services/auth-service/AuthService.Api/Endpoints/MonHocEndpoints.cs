using AuthService.Api.Dtos;
using AuthService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace AuthService.Api.Endpoints;

/// <summary>Rà soát Lần XVI (2026-08-21) — CRUD Môn học, cùng vị trí/quy ước với KhoaLopEndpoints
/// (gọi trực tiếp từ frontend qua gateway, không qua admin-service — chưa có audit log tương ứng,
/// ngoài phạm vi đợt này). Toàn bộ Admin-only — panel "Quản lý Môn học" không có khái niệm Teacher
/// tự quản lý môn học của mình.</summary>
public static class MonHocEndpoints
{
    public static IEndpointRouteBuilder MapMonHocEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth/mon-hoc").WithTags("MonHoc")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        group.MapGet("/", async (IMonHocService service, CancellationToken ct) =>
        {
            var result = await service.ListAsync(ct);
            return Results.Ok(ApiResponse<IReadOnlyList<MonHocResponse>>.Ok(result));
        });

        group.MapPost("/", async (CreateMonHocRequest request, IMonHocService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(request, ct);
                return Results.Created($"/api/v1/auth/mon-hoc/{result.Id}", ApiResponse<MonHocResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<CreateMonHocRequest>>();

        group.MapPut("/{id:guid}", async (Guid id, UpdateMonHocRequest request, IMonHocService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(id, request, ct);
                return Results.Ok(ApiResponse<MonHocResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<UpdateMonHocRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IMonHocService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.Ok(ApiResponse.Ok());
        });

        group.MapPut("/{id:guid}/lop", async (Guid id, AssignMonHocLopRequest request, IMonHocService service, CancellationToken ct) =>
            {
                var result = await service.AssignLopAsync(id, request.LopIds, ct);
                return Results.Ok(ApiResponse<MonHocResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<AssignMonHocLopRequest>>();

        return app;
    }
}
