using QuizService.Api.Dtos;

namespace QuizService.Api.Export;

public interface IWordExportService
{
    Task<byte[]> ExportAsync(ExportWordRequest request, CancellationToken ct);
}
