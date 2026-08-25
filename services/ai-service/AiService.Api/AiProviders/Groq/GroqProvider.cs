using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AiService.Api.AiProviders.Groq;

/// <summary>
/// Groq's implementation of IAiProvider. Owns Groq's wire format (request/response DTOs), Groq's
/// error shapes (429 tokens-per-minute, 413 payload-too-large — see remarks on SendAsync), and
/// Groq's json_mode strategy (prompt-engineered, not a native API flag — see CompleteJsonAsync).
/// None of this leaks past this class: AiProviderRouter and every business Service only ever see
/// AiMessage / AiProviderTransientException / AiProviderPermanentException.
/// </summary>
public sealed partial class GroqProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly AiProviderConfig _config;

    public string Name => _config.Name;

    public GroqProvider(HttpClient httpClient, AiProviderConfig config, string apiKey)
    {
        _config = config;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(config.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public Task<string> ChatAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct) =>
        SendAsync(messages, maxTokens, ct);

    public Task<string> CompleteTextAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct) =>
        SendAsync(messages, maxTokens, ct);

    /// <summary>Groq has no native structured-output flag we use — the caller's prompt already asks
    /// for JSON (see e.g. QuestionExtractionService), so this just cleans up what commonly comes
    /// back: a ```json fence, or stray trailing text after an otherwise well-formed object. A
    /// future provider with a real json_object response_format could skip this step entirely and
    /// still satisfy the same IAiProvider contract.</summary>
    public async Task<string> CompleteJsonAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct)
    {
        var raw = await SendAsync(messages, maxTokens, ct);
        return MarkdownJson.ExtractFirstJsonValue(MarkdownJson.StripCodeFence(raw.Trim()));
    }

    private async Task<string> SendAsync(IReadOnlyList<AiMessage> messages, int maxTokens, CancellationToken ct)
    {
        var request = new GroqChatRequest(
            _config.Model,
            messages.Select(m => new GroqMessageDto(m.Role, m.Content)).ToList(),
            maxTokens);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/openai/v1/chat/completions", request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderTransientException(Name, $"Không kết nối được tới Groq: {ex.Message}", retryAfterSeconds: null);
        }

        // Tài khoản Groq gói on_demand có trần 8.000 token/phút (TPM), áp dụng chung cho MỌI model
        // — xác nhận qua test thật 2026-08-18 (xem [[project ai-service Groq TPM finding]]). 429 =
        // vượt trần token/phút (tạm thời, retry được — Groq tự cho biết chờ bao lâu qua header
        // Retry-After hoặc trong nội dung lỗi "...try again in Xs"). 413 = riêng request này đã
        // vượt trần dù tài khoản đang rảnh (retry vô ích trên Groq, nhưng router vẫn có thể thử
        // provider khác có context window lớn hơn).
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var retryAfter = ParseRetryAfterSeconds(response, errorBody);
            throw new AiProviderTransientException(
                Name, $"Groq đã đạt giới hạn tốc độ (token/phút), cần chờ khoảng {retryAfter:F0}s rồi thử lại.", retryAfter);
        }

        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            throw new AiProviderPermanentException(
                Name, "Nội dung gửi cho Groq trong 1 lượt gọi quá lớn, vượt giới hạn của tài khoản.");
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new AiProviderTransientException(Name, $"Groq đang gặp sự cố (HTTP {(int)response.StatusCode}).", retryAfterSeconds: null);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new AiProviderPermanentException(Name, $"Groq từ chối yêu cầu (HTTP {(int)response.StatusCode}): {body}");
        }

        var responseBody = await response.Content.ReadFromJsonAsync<GroqChatResponseDto>(cancellationToken: ct)
            ?? throw new AiProviderPermanentException(Name, "Groq trả về phản hồi rỗng.");

        var content = responseBody.Choices.FirstOrDefault()?.Message.Content
            ?? throw new AiProviderPermanentException(Name, "Groq trả về phản hồi không có nội dung.");

        return content;
    }

    private static double? ParseRetryAfterSeconds(HttpResponseMessage response, string errorBody)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta.TotalSeconds;
        }

        var match = RetryAfterPattern().Match(errorBody);
        return match.Success && double.TryParse(match.Groups[1].Value, out var seconds) ? seconds : 5.0;
    }

    [GeneratedRegex(@"try again in ([\d.]+)s")]
    private static partial Regex RetryAfterPattern();

    private sealed record GroqChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<GroqMessageDto> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record GroqMessageDto(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record GroqChatResponseDto([property: JsonPropertyName("choices")] IReadOnlyList<GroqChoiceDto> Choices);

    private sealed record GroqChoiceDto([property: JsonPropertyName("message")] GroqResponseMessageDto Message);

    private sealed record GroqResponseMessageDto([property: JsonPropertyName("content")] string Content);
}
