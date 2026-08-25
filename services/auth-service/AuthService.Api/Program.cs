using System.Reflection;
using AuthService.Api.Data;
using AuthService.Api.Endpoints;
using AuthService.Api.Services;
using AuthService.Api.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Data;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.HealthChecks;
using Shared.Infrastructure.Observability;
using Shared.Infrastructure.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedObservability("auth-service");

var connectionString = builder.Configuration.GetConnectionString("AuthDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:AuthDb configuration.");

builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSharedHealthChecksSqlServer(connectionString);

builder.Services.AddSharedJwtAuthentication(builder.Configuration);
builder.Services.AddSharedJwtIssuer();
builder.Services.AddSharedCors(builder.Configuration);
builder.Services.AddSharedValidation(Assembly.GetExecutingAssembly());
builder.Services.AddInternalServiceAuth(builder.Configuration);

builder.Services.AddScoped<IAuthService, AuthServiceImpl>();
builder.Services.AddScoped<IKhoaLopService, KhoaLopService>();
builder.Services.AddScoped<IMonHocService, MonHocService>();

// Avatar upload — dùng lại đúng 1 Cloudinary account platform-owned với content-service (xem
// CloudinaryOptions remarks), thư mục riêng "tthcm/avatars". Giới hạn body nhỏ hơn hẳn
// content-service's 60MB vì avatar tối đa 5MB (validate lại ở endpoint, đây chỉ là trần Kestrel).
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection(CloudinaryOptions.SectionName));
builder.Services.AddScoped<IAvatarStorage, CloudinaryAvatarStorage>();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 6_000_000);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 6_000_000);

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

app.MapAuthEndpoints();
app.MapKhoaLopEndpoints();
app.MapMonHocEndpoints();
app.MapSharedHealthChecks();

var autoMigrate = builder.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment());
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await DatabaseInitializer.MigrateWithRetryAsync(db, app.Logger);

    if (builder.Configuration.GetValue<bool>("Seed:Enabled"))
    {
        await DemoDataSeeder.SeedAsync(db);
    }
}

app.Run();

public partial class Program;
