using AiService.Api.Dtos;

namespace AiService.Api.Services;

public interface ILectureService
{
    Task<GenerateLectureResponse> GenerateLectureAsync(GenerateLectureRequest request, CancellationToken ct);
    Task<GenerateComprehensionQuestionsResponse> GenerateComprehensionQuestionsAsync(GenerateComprehensionQuestionsRequest request, CancellationToken ct);
}
