using AiService.Api.Dtos;

namespace AiService.Api.Services;

public interface IChatService
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct);
}
