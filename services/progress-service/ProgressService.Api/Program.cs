using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ProgressService.Api.Clients;
using ProgressService.Api.Data;
using ProgressService.Api.Endpoints;
using ProgressService.Api.Services;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Data;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.HealthChecks;
using Shared.Infrastructure.Observability;
using Shared.Infrastructure.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedObservability("progress-service");

var connectionString = builder.Configuration.GetConnectionString("ProgressDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:ProgressDb configuration.");

builder.Services.AddDbContext<ProgressDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSharedHealthChecksSqlServer(connectionString);

builder.Services.AddSharedJwtAuthentication(builder.Configuration);
builder.Services.AddSharedCors(builder.Configuration);
builder.Services.AddSharedValidation(Assembly.GetExecutingAssembly());
builder.Services.AddInternalServiceAuth(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IStudyLogService, StudyLogService>();
builder.Services.AddScoped<IStudentProgressService, StudentProgressService>();
builder.Services.AddScoped<ILopDataAdminService, LopDataAdminService>();

var authServiceBaseUrl = builder.Configuration.GetValue("Services:Auth:BaseUrl", "http://auth-service:8080")!;
builder.Services.AddHttpClient<IUserNameLookupClient, HttpUserNameLookupClient>(client =>
{
    client.BaseAddress = new Uri(authServiceBaseUrl);
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

app.MapProgressEndpoints();
app.MapLopDataAdminEndpoints();
app.MapSharedHealthChecks();

if (builder.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ProgressDbContext>();
    await DatabaseInitializer.MigrateWithRetryAsync(db, app.Logger);
}

app.Run();

public partial class Program;
