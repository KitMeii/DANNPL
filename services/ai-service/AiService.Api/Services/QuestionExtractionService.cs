using System.Text.Json;
using System.Text.Json.Serialization;
using AiService.Api.AiProviders;
using AiService.Api.Caching;
using AiService.Api.Dtos;

namespace AiService.Api.Services;

/// <summary>
/// Generates candidate MCQ questions from a teacher-uploaded document. This never writes to
/// quiz-service's question bank itself — the teacher reviews the generated questions in the UI
/// first, then quiz-service's own POST /api/v1/quiz/questions (Teacher/Admin only) is what
/// actually persists the ones they accept.
/// </summary>
public sealed class QuestionExtractionService(IAiProviderRouter aiRouter, ResponseCache cache) : IQuestionExtractionService
{
    // AllowTrailingCommas: Groq occasionally emits a trailing comma before the closing ]/} in its
    // JSON output (observed in generate-exam-set responses) — tolerate it rather than 500ing on an
    // otherwise well-formed response.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { AllowTrailingCommas = true };

    public Task<ExtractQuestionsResponse> ExtractAsync(ExtractQuestionsRequest request, CancellationToken ct) =>
        cache.GetOrCreateAsync("extract-questions", $"{request.Chapter}|{request.Count}|{request.SourceText}", async () =>
        {
            var prompt =
                $"Từ nội dung tài liệu chương \"{request.Chapter}\" sau đây, hãy soạn {request.Count} câu " +
                "hỏi trắc nghiệm 4 lựa chọn (A/B/C/D) bằng tiếng Việt, kèm đáp án đúng và giải thích ngắn " +
                "gọn. Với MỖI câu, tự đánh giá độ khó (1=dễ, 2=trung bình, 3=khó) và gán 1 chủ đề/topic " +
                "ngắn gọn (2-5 từ) mô tả nội dung câu hỏi đó thuộc phần nào của chương, để sau này có thể " +
                "nhóm/lọc câu hỏi theo độ khó và chủ đề. Trả lời CHỈ bằng một JSON array, không thêm văn " +
                "bản nào khác, đúng định dạng: " +
                "[{\"question\": \"...\", \"optionA\": \"...\", \"optionB\": \"...\", \"optionC\": \"...\", " +
                "\"optionD\": \"...\", \"correctAnswer\": <0=A,1=B,2=C,3=D>, \"explanation\": \"...\", " +
                "\"difficulty\": <1|2|3>, \"topic\": \"...\"}, ...]" +
                "\n\nNội dung tài liệu:\n" + request.SourceText;

            var json = await aiRouter.CompleteJsonAsync([new AiMessage("user", prompt)], maxTokens: 4000, ct);
            var payload = ParsePayload(json);

            var questions = payload.Select(q => new ExtractedQuestion(
                q.Question, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.CorrectAnswer, q.Explanation,
                q.Difficulty, q.Topic)).ToList();

            return new ExtractQuestionsResponse(questions);
        });

    /// <summary>Sinh cả câu trắc nghiệm lẫn tự luận trong MỘT lần gọi LLM (một prompt, một JSON
    /// response chứa 2 mảng) — rẻ hơn và đơn giản hơn 2 lần gọi riêng, và khớp với ý nghĩa "một đề
    /// kiểm tra" thay vì 2 tính năng độc lập. Cũng không tự lưu — cùng nguyên tắc review-trước-khi-
    /// persist với ExtractAsync ở trên.</summary>
    public Task<GenerateExamSetResponse> GenerateExamSetAsync(GenerateExamSetRequest request, CancellationToken ct) =>
        cache.GetOrCreateAsync("generate-exam-set",
            $"{request.Chapter}|{request.McqCount}|{request.EssayCount}|{request.SourceText}", async () =>
        {
            // 0 = giáo viên không cần loại câu hỏi đó (audit 2026-08-16 việc 3) — không đưa vào
            // prompt luôn, tiết kiệm quota thay vì sinh ra rồi bỏ. Validator đã chặn cả 2 cùng = 0.
            var wantsMcq = request.McqCount > 0;
            var wantsEssay = request.EssayCount > 0;

            var typeParts = new List<string>();
            if (wantsMcq) typeParts.Add($"{request.McqCount} câu hỏi trắc nghiệm 4 lựa chọn (A/B/C/D)");
            if (wantsEssay) typeParts.Add($"{request.EssayCount} câu hỏi tự luận");

            var schemaParts = new List<string>();
            if (wantsMcq)
                schemaParts.Add(
                    "\"mcq\": [{\"question\": \"...\", \"optionA\": \"...\", \"optionB\": \"...\", \"optionC\": \"...\", " +
                    "\"optionD\": \"...\", \"correctAnswer\": <0=A,1=B,2=C,3=D>, \"explanation\": \"...\"}, ...]");
            if (wantsEssay)
                schemaParts.Add("\"essay\": [{\"question\": \"...\", \"suggestedAnswer\": \"...\"}, ...]");

            var prompt =
                $"Từ nội dung tài liệu chương \"{request.Chapter}\" sau đây, hãy soạn một đề kiểm tra gồm " +
                $"{string.Join(" và ", typeParts)}, tất cả bằng tiếng Việt. " +
                (wantsMcq ? "Với mỗi câu trắc nghiệm, kèm đáp án đúng và giải thích ngắn gọn. " : "") +
                (wantsEssay ? "Với mỗi câu tự luận, kèm một đáp án gợi ý để giáo viên tham khảo khi chấm. " : "") +
                "Trả lời CHỈ bằng một JSON object, không thêm văn bản nào khác, đúng định dạng: " +
                "{" + string.Join(", ", schemaParts) + "}" +
                "\n\nNội dung tài liệu:\n" + request.SourceText;

            var json = await aiRouter.CompleteJsonAsync([new AiMessage("user", prompt)], maxTokens: 4000, ct);
            var payload = ParseExamSetPayload(json);

            var mcq = wantsMcq
                ? (payload.Mcq ?? []).Select(q => new ExtractedQuestion(
                    q.Question, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.CorrectAnswer, q.Explanation)).ToList()
                : [];
            var essay = wantsEssay
                ? (payload.Essay ?? []).Select(q => new GeneratedEssayQuestion(q.Question, q.SuggestedAnswer)).ToList()
                : [];

            return new GenerateExamSetResponse(mcq, essay);
        });

    // "Kiểm tra nhanh kiến thức" (Student, giang-bai.html, audit 2026-08-16 mục 3) — công cụ tự học
    // cá nhân, KHÔNG lưu DB, KHÔNG qua duyệt giáo viên (không cần, vì không có gì được lưu). Số câu
    // cố định phía server (không cho client tùy chỉnh) để giữ prompt/chi phí gọn. TTL cache ngắn
    // hơn hẳn các luồng khác (5 phút thay vì mặc định 1 giờ) — nhiều học viên mở cùng 1 tài liệu
    // trong vài phút dùng chung 1 lần gọi Groq thật, chấp nhận thấy câu hỏi giống nhau vì đây là
    // tự kiểm tra, không phải bài thi.
    private const int QuickCheckQuestionCount = 4;
    private static readonly TimeSpan QuickCheckCacheTtl = TimeSpan.FromMinutes(5);

    public Task<QuickCheckResponse> QuickCheckAsync(QuickCheckRequest request, CancellationToken ct) =>
        cache.GetOrCreateAsync("quick-check", $"{request.MaterialId}|{request.Chapter}|{request.SourceText}", async () =>
        {
            var prompt =
                $"Từ nội dung tài liệu chương \"{request.Chapter}\" sau đây, hãy soạn {QuickCheckQuestionCount} " +
                "câu hỏi trắc nghiệm 4 lựa chọn (A/B/C/D) ngắn gọn bằng tiếng Việt để học viên tự kiểm tra " +
                "nhanh mức độ hiểu bài, kèm đáp án đúng và giải thích ngắn gọn. Trả lời CHỈ bằng một JSON " +
                "array, không thêm văn bản nào khác, đúng định dạng: " +
                "[{\"question\": \"...\", \"optionA\": \"...\", \"optionB\": \"...\", \"optionC\": \"...\", " +
                "\"optionD\": \"...\", \"correctAnswer\": <0=A,1=B,2=C,3=D>, \"explanation\": \"...\"}, ...]" +
                "\n\nNội dung tài liệu:\n" + request.SourceText;

            var json = await aiRouter.CompleteJsonAsync([new AiMessage("user", prompt)], maxTokens: 1500, ct);
            var payload = ParsePayload(json);

            var questions = payload.Select(q => new ExtractedQuestion(
                q.Question, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.CorrectAnswer, q.Explanation)).ToList();

            return new QuickCheckResponse(questions);
        }, QuickCheckCacheTtl);

    // Nhận thẳng chuỗi JSON đã sạch từ aiRouter.CompleteJsonAsync (provider tự lo strip code
    // fence/rác thừa — xem IAiProvider.CompleteJsonAsync remarks), không còn tự xử lý ở đây nữa.
    private static List<QuestionPayload> ParsePayload(string jsonText)
    {
        try
        {
            return JsonSerializer.Deserialize<List<QuestionPayload>>(jsonText, JsonOptions)
                ?? throw new InvalidOperationException("AI question-extraction response deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"AI question-extraction response was not valid JSON: {jsonText}", ex);
        }
    }

    private static ExamSetPayload ParseExamSetPayload(string jsonText)
    {
        try
        {
            return JsonSerializer.Deserialize<ExamSetPayload>(jsonText, JsonOptions)
                ?? throw new InvalidOperationException("AI exam-set-generation response deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"AI exam-set-generation response was not valid JSON: {jsonText}", ex);
        }
    }

    private sealed record QuestionPayload(
        [property: JsonPropertyName("question")] string Question,
        [property: JsonPropertyName("optionA")] string OptionA,
        [property: JsonPropertyName("optionB")] string OptionB,
        [property: JsonPropertyName("optionC")] string OptionC,
        [property: JsonPropertyName("optionD")] string OptionD,
        [property: JsonPropertyName("correctAnswer")] int CorrectAnswer,
        [property: JsonPropertyName("explanation")] string? Explanation,
        [property: JsonPropertyName("difficulty")] int? Difficulty = null,
        [property: JsonPropertyName("topic")] string? Topic = null);

    private sealed record EssayPayload(
        [property: JsonPropertyName("question")] string Question,
        [property: JsonPropertyName("suggestedAnswer")] string? SuggestedAnswer);

    // Nullable: khi McqCount/EssayCount = 0, prompt không yêu cầu key đó nữa (audit 2026-08-16
    // việc 3) — AI có thể bỏ hẳn key thay vì trả mảng rỗng, .Mcq/.Essay khi đó null thay vì [].
    private sealed record ExamSetPayload(
        [property: JsonPropertyName("mcq")] List<QuestionPayload>? Mcq = null,
        [property: JsonPropertyName("essay")] List<EssayPayload>? Essay = null);
}
