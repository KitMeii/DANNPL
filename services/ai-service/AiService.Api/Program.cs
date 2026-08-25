using System.Reflection;
using AiService.Api.AiProviders;
using AiService.Api.AiProviders.Groq;
using AiService.Api.Caching;
using AiService.Api.Endpoints;
using AiService.Api.Services;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Common;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.HealthChecks;
using Shared.Infrastructure.Observability;
using Shared.Infrastructure.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.AddSharedObservability("ai-service");

builder.Services.AddSharedHealthChecks(connectionString: null);
builder.Services.AddSharedJwtAuthentication(builder.Configuration);
builder.Services.AddSharedCors(builder.Configuration);
builder.Services.AddSharedValidation(Assembly.GetExecutingAssembly());

builder.Services.Configure<AiProvidersOptions>(builder.Configuration.GetSection(AiProvidersOptions.SectionName));
builder.Services.Configure<SubjectOptions>(builder.Configuration.GetSection(SubjectOptions.SectionName));
// Named (not typed) HttpClient — AiProviderFactory builds each IAiProvider manually from config
// (Name/Model/BaseUrl/resolved API key aren't DI-resolvable generically), so it asks
// IHttpClientFactory for a plain client per provider type instead of relying on AddHttpClient<T>'s
// constructor-injection magic. Adding a new provider needs one more AddHttpClient(nameof(...)) line
// here + a case in AiProviderFactory — see AiProviders/README.md.
builder.Services.AddHttpClient(nameof(GroqProvider));
builder.Services.AddSingleton<IAiProviderFactory, AiProviderFactory>();
builder.Services.AddSingleton<IAiProviderRouter, AiProviderRouter>();

var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "ai-service:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSingleton<ResponseCache>();

builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ILectureService, LectureService>();
builder.Services.AddScoped<IOralGradingService, OralGradingService>();
builder.Services.AddScoped<IQuestionExtractionService, QuestionExtractionService>();

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

app.MapAiEndpoints();
app.MapSharedHealthChecks();

app.Run();

public partial class Program;
