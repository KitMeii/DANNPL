using AiService.Api.Dtos;

namespace AiService.Api.Services;

public interface IOralGradingService
{
    Task<GradeOralResponse> GradeAsync(GradeOralRequest request, CancellationToken ct);
}
