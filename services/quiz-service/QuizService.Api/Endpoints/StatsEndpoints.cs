using System.Security.Claims;
using QuizService.Api.Dtos;
using QuizService.Api.Services;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Validation;

namespace QuizService.Api.Endpoints;

/// <summary>
/// Endpoint tổng hợp điểm theo danh sách UserId — dùng cho thống kê điểm TB theo Lớp/Khóa ở
/// admin-service (Bước C, mở rộng Gap 2 cho Teacher). Đây là lời gọi service-to-service
/// (admin-service gọi vào, không phải client trực tiếp), nhận nguyên 1 danh sách UserId bất kỳ chứ
/// không tự suy ra từ JWT của người gọi — giống hệt lý do progress-service/record-score cần
/// RequireInternalServiceKeyFilter (Phần A RBAC audit): 1 JWT hợp lệ một mình không đủ, còn cần
/// biết InternalService:SharedKey. RequireRole ở đây chỉ là lớp lọc thô theo vai trò (Admin hoặc
/// Teacher) — biên bảo mật THẬT của endpoint này là X-Internal-Key (chỉ admin-service biết), vì
/// quiz-service không có dữ liệu Lop/GiaoVienId để tự kiểm tra "đúng GV chủ nhiệm". Việc UserId nào
/// được hỏi điểm đã được LopKhoaStatsService (admin-service) kiểm tra quyền sở hữu lớp TRƯỚC khi
/// gọi tới đây (chỉ list học viên của đúng lopId đã xác minh), nên cho Teacher gọi ở đây an toàn.
/// </summary>
public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quiz/stats").WithTags("QuizStats");

        group.MapPost("/scores-by-users", async (ScoresByUsersRequest request, IQuizStatsService service, CancellationToken ct) =>
            {
                var result = await service.GetScoresByUsersAsync(request.UserIds, ct);
                return Results.Ok(ApiResponse<ScoresByUsersResponse>.Ok(result));
            })
            .AddEndpointFilter<ValidationEndpointFilter<ScoresByUsersRequest>>()
            .AddEndpointFilter<RequireInternalServiceKeyFilter>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        // Việc C (2026-08-16) — bảng xếp hạng theo Lớp. Mọi role đã đăng nhập được, không
        // RequireRole tĩnh — RBAC thật (Student đúng lớp mình / Teacher đúng lớp mình phụ trách)
        // phụ thuộc dữ liệu nên nằm trong GetLopLeaderboardAsync, cùng nguyên tắc với
        // KhoaLopService.ListHocVienAsync/ChangeChucVuAsync ở auth-service.
        group.MapGet("/leaderboard-by-lop", async (Guid lopId, ClaimsPrincipal principal, IQuizStatsService service, CancellationToken ct) =>
            {
                var result = await service.GetLopLeaderboardAsync(lopId, principal.GetUserId(), principal.GetRole(), ct);
                return Results.Ok(ApiResponse<LopLeaderboardResponse>.Ok(result));
            })
            .RequireAuthorization();

        return app;
    }
}
