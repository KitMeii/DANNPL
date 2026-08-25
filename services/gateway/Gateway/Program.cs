using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.HealthChecks;
using Shared.Infrastructure.Middleware;
using Shared.Infrastructure.Observability;
using Yarp.ReverseProxy.Transforms;

const string AiRateLimiterPolicy = "ai";
const string AuthenticatedPolicy = "authenticated";
const string AdminOnlyPolicy = "admin-only";
const string TeacherOrAdminPolicy = "teacher-or-admin";

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedObservability("gateway");

builder.Services.AddSharedJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthenticatedPolicy, policy => policy.RequireAuthenticatedUser())
    .AddPolicy(AdminOnlyPolicy, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin))
    // Gap 2: GET /api/v1/admin/stats/lop/{lopId} — Teacher chủ nhiệm cần gọi được, không chỉ
    // Admin. Route-cụ-thể riêng trong appsettings.json ("admin-stats-lop-route") dùng policy này
    // thay vì "admin-only" của route catch-all "/api/v1/admin/{**catch-all}" bên dưới nó — YARP/
    // ASP.NET Core routing tự ưu tiên route có tiền tố literal cụ thể hơn route catch-all, không
    // cần khai báo Order. admin-service tự kiểm tra thêm "đúng GV chủ nhiệm lopId đó" ở tầng
    // service (data-dependent, xem LopKhoaStatsService remarks) — policy ở gateway này chỉ lọc
    // thô theo Role, không biết gì về lopId.
    .AddPolicy(TeacherOrAdminPolicy, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Teacher, Roles.Admin));

builder.Services.AddSharedCors(builder.Configuration);
builder.Services.AddSharedHealthChecks(connectionString: null);

// Rate limiting on /api/v1/ai/** only — this is the route that spends real Groq API money, so it
// gets its own per-user (or per-IP for anonymous) fixed-window budget separate from every other route.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AiRateLimiterPolicy, httpContext =>
    {
        var partitionKey = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.User.FindFirstValue("sub")
            : httpContext.Connection.RemoteIpAddress?.ToString();

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey ?? "anonymous", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:Ai:PermitLimit", 10),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilderContext =>
    {
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            var correlationId = transformContext.HttpContext.GetCorrelationId();
            transformContext.ProxyRequest.Headers.Remove(HeaderNames.CorrelationId);
            transformContext.ProxyRequest.Headers.Add(HeaderNames.CorrelationId, correlationId);

            var user = transformContext.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
                var role = user.FindFirstValue(ClaimTypes.Role);

                if (!string.IsNullOrEmpty(userId))
                {
                    transformContext.ProxyRequest.Headers.Remove(HeaderNames.UserId);
                    transformContext.ProxyRequest.Headers.Add(HeaderNames.UserId, userId);
                }

                if (!string.IsNullOrEmpty(role))
                {
                    transformContext.ProxyRequest.Headers.Remove(HeaderNames.UserRole);
                    transformContext.ProxyRequest.Headers.Add(HeaderNames.UserRole, role);
                }
            }

            return ValueTask.CompletedTask;
        });
    });
// Active health checks (cluster "HealthCheck.Active" in appsettings) are enabled purely via
// config — YARP 2.2's AddReverseProxy() already registers the active health check monitor and
// its default "ConsecutiveFailures" policy, no extra call needed.

var app = builder.Build();

app.UseSharedMiddleware();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();
app.MapSharedHealthChecks();

app.Run();
