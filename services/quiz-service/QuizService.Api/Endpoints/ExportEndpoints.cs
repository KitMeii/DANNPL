using QuizService.Api.Dtos;
using QuizService.Api.Export;
using Shared.Contracts;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Validation;

namespace QuizService.Api.Endpoints;

public static class ExportEndpoints
{
    private const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/quiz/export").WithTags("Export").RequireAuthorization();

        group.MapPost("/word", async (ExportWordRequest request, IWordExportService service, CancellationToken ct) =>
            {
                var bytes = await service.ExportAsync(request, ct);
                return Results.File(bytes, WordContentType, "de-thi.docx");
            })
            .AddEndpointFilter<ValidationEndpointFilter<ExportWordRequest>>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.Teacher, Roles.Admin));

        return app;
    }
}
