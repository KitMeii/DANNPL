namespace AiService.Api.AiProviders;

/// <summary>Provider-agnostic chat message — replaces the old Groq-specific GroqMessage so business
/// services don't depend on any one vendor's wire format.</summary>
public sealed record AiMessage(string Role, string Content);
