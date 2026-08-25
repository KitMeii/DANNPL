namespace AiService.Api.Dtos;

/// <summary>Role is restricted to "user"/"assistant" by the validator — a client can never inject
/// its own "system" message to override the server-side system prompt.</summary>
public sealed record ChatMessage(string Role, string Content);

public sealed record ChatRequest(List<ChatMessage> Messages);

public sealed record ChatResponse(string Reply);
