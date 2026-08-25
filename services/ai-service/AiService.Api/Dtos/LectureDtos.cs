namespace AiService.Api.Dtos;

/// <summary>
/// PartIndex/PartTotal/PreviousTail support chunked lecture generation (audit 2026-08-18, Groq
/// 8.000 TPM finding) — a long document is split client-side into ≤6.000-char pieces, each sent as
/// its own request so no single call risks the account's tokens-per-minute cap. Defaults
/// (PartIndex=0, PartTotal=1) preserve the original single-call behavior for short documents.
/// </summary>
public sealed record GenerateLectureRequest(
    string Chapter,
    string Topic,
    string SourceText,
    int PartIndex = 0,
    int PartTotal = 1,
    string PreviousTail = "");

public sealed record GenerateLectureResponse(string Content);

public sealed record GenerateComprehensionQuestionsRequest(string Chapter, string SourceText);

public sealed record GenerateComprehensionQuestionsResponse(List<string> Questions);
