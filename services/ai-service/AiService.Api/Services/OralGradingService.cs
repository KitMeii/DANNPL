using System.Text.Json;
using System.Text.Json.Serialization;
using AiService.Api.AiProviders;
using AiService.Api.Dtos;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Common;

namespace AiService.Api.Services;

public sealed class OralGradingService(IAiProviderRouter aiRouter, IOptions<SubjectOptions> subjectOptions) : IOralGradingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GradeOralResponse> GradeAsync(GradeOralRequest request, CancellationToken ct)
    {
        var followups = request.FollowupAnswers.Count > 0
            ? string.Join("\n", request.FollowupAnswers.Select((a, i) => $"Bổ sung {i + 1}: {a}"))
            : "(không có)";

        var prompt =
            $"Bạn là giám khảo chấm thi vấn đáp {subjectOptions.Value.SubjectName}. Chấm câu trả lời sau theo 4 " +
            "tiêu chí (thang điểm 0-10 mỗi tiêu chí): noi_dung (nội dung), lap_luan (lập luận), " +
            "vi_du (ví dụ minh hoạ), dien_dat (diễn đạt).\n\n" +
            $"Câu hỏi: {request.QuestionText}\n" +
            $"Đáp án tham khảo: {request.ExpectedAnswer ?? "(không có)"}\n" +
            $"Câu trả lời của học viên: {request.MainAnswer}\n" +
            $"Câu trả lời bổ sung:\n{followups}\n\n" +
            "Trả lời CHỈ bằng một JSON object duy nhất, không thêm bất kỳ văn bản nào khác, đúng " +
            "định dạng: {\"score\": <điểm trung bình 0-10, 2 chữ số thập phân>, \"comment\": \"<nhận " +
            "xét ngắn gọn bằng tiếng Việt>\", \"rubric\": {\"noi_dung\": <0-10>, \"lap_luan\": <0-10>, " +
            "\"vi_du\": <0-10>, \"dien_dat\": <0-10>}}";

        // aiRouter.CompleteJsonAsync — provider (GroqProvider) đảm bảo trả JSON sạch, không còn
        // phải tự strip code fence ở tầng service nữa (xem IAiProvider.CompleteJsonAsync remarks).
        var json = await aiRouter.CompleteJsonAsync([new AiMessage("user", prompt)], maxTokens: 500, ct);
        var payload = ParsePayload(json);

        return new GradeOralResponse(payload.Score, payload.Comment, payload.Rubric);
    }

    private static GradingPayload ParsePayload(string jsonText)
    {
        try
        {
            return JsonSerializer.Deserialize<GradingPayload>(jsonText, JsonOptions)
                ?? throw new InvalidOperationException("AI grading response deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"AI grading response was not valid JSON: {jsonText}", ex);
        }
    }

    private sealed record GradingPayload(
        [property: JsonPropertyName("score")] decimal Score,
        [property: JsonPropertyName("comment")] string? Comment,
        [property: JsonPropertyName("rubric")] Dictionary<string, decimal> Rubric);
}
