using AiService.Api.Dtos;

namespace AiService.Api.Services;

public interface IQuestionExtractionService
{
    Task<ExtractQuestionsResponse> ExtractAsync(ExtractQuestionsRequest request, CancellationToken ct);
    Task<GenerateExamSetResponse> GenerateExamSetAsync(GenerateExamSetRequest request, CancellationToken ct);
    Task<QuickCheckResponse> QuickCheckAsync(QuickCheckRequest request, CancellationToken ct);
}
