using System.Reflection;
using AdminService.Api.Caching;
using AdminService.Api.Clients;
using AdminService.Api.Data;
using AdminService.Api.Endpoints;
using AdminService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Data;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.HealthChecks;
using Shared.Infrastructure.Observability;
using Shared.Infrastructure.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedObservability("admin-service");

var connectionString = builder.Configuration.GetConnectionString("AdminDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:AdminDb configuration.");

builder.Services.AddDbContext<AdminDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSharedHealthChecksSqlServer(connectionString);

builder.Services.AddSharedJwtAuthentication(builder.Configuration);
builder.Services.AddSharedCors(builder.Configuration);
builder.Services.AddSharedValidation(Assembly.GetExecutingAssembly());
builder.Services.AddInternalServiceAuth(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<ISystemConfigService, SystemConfigService>();
builder.Services.AddScoped<ISystemOverviewService, SystemOverviewService>();
builder.Services.AddScoped<ISystemStatsClient, HttpSystemStatsClient>();
builder.Services.AddScoped<ILopKhoaStatsService, LopKhoaStatsService>();
builder.Services.AddScoped<ITeacherOverviewService, TeacherOverviewService>();
builder.Services.AddScoped<ILopDeletionService, LopDeletionService>();

// Việc 7 — cache cho TeacherOverviewService (nhiều lượt gọi cross-service). Cùng pattern
// AiService.Api's ResponseCache: Redis khi có Redis:ConnectionString, in-process fallback khi không.
var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "admin-service:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddSingleton<ResponseCache>();

var authServiceBaseUrl = builder.Configuration.GetValue("Services:Auth:BaseUrl", "http://auth-service:8080")!;
builder.Services.AddHttpClient<IAuthAdminClient, HttpAuthAdminClient>(client =>
{
    client.BaseAddress = new Uri(authServiceBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var quizServiceBaseUrlForStats = builder.Configuration.GetValue("Services:Quiz:BaseUrl", "http://quiz-service:8080")!;
builder.Services.AddHttpClient<IQuizStatsClient, HttpQuizStatsClient>(client =>
{
    client.BaseAddress = new Uri(quizServiceBaseUrlForStats);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Việc 4.2 mục 3 (2026-08-19) — client mới cho dump/xóa dữ liệu Lớp. Timeout dài hơn các client
// khác (30s thay vì 10s) vì DumpAsync có thể trả về khối lượng dữ liệu lớn (toàn bộ lịch sử kết
// quả của 1 Lớp) — không muốn timeout giữa chừng bước BACKUP (an toàn hơn timeout sớm ở bước đọc
// so với timeout ở bước xóa).
builder.Services.AddHttpClient<IQuizLopDataClient, HttpQuizLopDataClient>(client =>
{
    client.BaseAddress = new Uri(quizServiceBaseUrlForStats);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var progressServiceBaseUrl = builder.Configuration.GetValue("Services:Progress:BaseUrl", "http://progress-service:8080")!;
builder.Services.AddHttpClient<IProgressLopDataClient, HttpProgressLopDataClient>(client =>
{
    client.BaseAddress = new Uri(progressServiceBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("content-service", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue("Services:Content:BaseUrl", "http://content-service:8080")!);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("quiz-service", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue("Services:Quiz:BaseUrl", "http://quiz-service:8080")!);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSharedMiddleware();
app.UseAuthentication();
app.UseAuthorization();

app.MapAdminEndpoints();
app.MapSharedHealthChecks();

if (builder.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    await DatabaseInitializer.MigrateWithRetryAsync(db, app.Logger);
}

app.Run();

public partial class Program;
