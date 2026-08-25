namespace AiService.Api.AiProviders.Groq;

/// <summary>LLMs frequently wrap JSON answers in a ```json ... ``` fence even when told not to —
/// strip it before deserializing. Groq-specific cleanup, used only by GroqProvider.CompleteJsonAsync
/// — a provider with native structured output wouldn't need this at all.</summary>
internal static class MarkdownJson
{
    public static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewline = text.IndexOf('\n');
        var withoutOpenFence = firstNewline >= 0 ? text[(firstNewline + 1)..] : text;
        var closingFenceIndex = withoutOpenFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0 ? withoutOpenFence[..closingFenceIndex].Trim() : withoutOpenFence.Trim();
    }

    /// <summary>LLMs occasionally append stray text/braces after an otherwise well-formed JSON value
    /// (more likely on larger, nested schemas — observed on the combined mcq+essay exam-set prompt),
    /// which fails strict deserialization even after StripCodeFence. Finds the first `{` or `[`
    /// (whichever appears first — GroqProvider.CompleteJsonAsync is called for BOTH object-shaped
    /// payloads like exam-set/grading AND array-shaped ones like question lists, so this must handle
    /// both) and walks depth (ignoring brackets/braces inside string literals) to the position where
    /// it returns to zero — the true end of that JSON value — and returns just that substring.
    /// Returns the input unchanged if no `{` or `[` is found.</summary>
    public static string ExtractFirstJsonValue(string text)
    {
        var start = -1;
        var open = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{' || text[i] == '[')
            {
                start = i;
                open = text[i];
                break;
            }
        }
        if (start < 0)
        {
            return text;
        }

        var close = open == '{' ? '}' : ']';
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    return text[start..(i + 1)];
                }
            }
        }

        return text[start..];
    }
}
