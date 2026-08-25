using AiService.Api.AiProviders;
using AiService.Api.Caching;
using AiService.Api.Dtos;

namespace AiService.Api.Services;

public sealed class LectureService(IAiProviderRouter aiRouter, ResponseCache cache) : ILectureService
{
    public Task<GenerateLectureResponse> GenerateLectureAsync(GenerateLectureRequest request, CancellationToken ct)
    {
        // PartIndex/PartTotal vào key cache — 2 chunk khác nhau của cùng 1 tài liệu có SourceText
        // khác nhau nên vốn đã tách key nhau, nhưng thêm cho rõ ràng + đúng cả trường hợp trùng
        // ngẫu nhiên nội dung giữa 2 chunk (VD 2 chunk cùng ngắn same text).
        var cacheKey = $"{request.Chapter}|{request.Topic}|{request.PartIndex}|{request.PartTotal}|{request.SourceText}";
        return cache.GetOrCreateAsync("lecture", cacheKey, async () =>
        {
            var prompt = BuildLecturePrompt(request);
            // Tài liệu ngắn (1 chunk, hành vi gốc): giữ maxTokens 4000 như cũ. Tài liệu chunk hóa:
            // 1500/chunk là đủ cho ~1/N nội dung và giữ tổng token/lượt gọi thấp (xem [[project
            // ai-service Groq TPM finding]] — mỗi chunk phải nằm gọn dưới trần 8.000 token/phút).
            // Chunk CUỐI cần thêm ngân sách (2000) — test thật 2026-08-18 cho thấy 1500 không đủ để
            // vừa giảng hết nội dung vừa viết phần tổng kết, khiến tổng kết bị cắt dở giữa chừng.
            var isLastChunk = request.PartTotal > 1 && request.PartIndex == request.PartTotal - 1;
            var maxTokens = request.PartTotal <= 1 ? 4000 : isLastChunk ? 2000 : 1500;
            var content = await aiRouter.CompleteTextAsync([new AiMessage("user", prompt)], maxTokens, ct);
            return new GenerateLectureResponse(content);
        });
    }

    // Việc 3.0 (2026-08-18) — cột phụ đề trên giao diện hiển thị TOÀN VĂN bản tóm tắt của phần
    // đang đọc, không phải phụ đề phim 1-2 dòng, nên khối văn bản dài liền mạch rất khó theo dõi
    // khi vừa nghe vừa đọc. Chỉ đổi PROMPT + cách render (giang-bai.html renderCaption) — KHÔNG
    // đổi chunking/pacing/retry đã test kỹ ở Phần A/B, KHÔNG đổi cơ chế TTS/highlight câu đang đọc.
    private const string ParagraphStructureInstruction =
        " Chia nội dung thành các đoạn văn NGẮN (mỗi đoạn khoảng 2-4 câu, xoay quanh 1 ý), xuống " +
        "dòng để trống 1 dòng giữa các đoạn — KHÔNG viết thành một khối văn bản dài liền mạch.";

    private static string BuildLecturePrompt(GenerateLectureRequest request)
    {
        if (request.PartTotal <= 1)
        {
            return $"Dựa trên nội dung sau của chương \"{request.Chapter}\" - chủ đề \"{request.Topic}\", " +
                "hãy viết một bài giảng dẫn dắt bằng tiếng Việt, giọng văn như giảng viên đang giảng bài " +
                "trực tiếp cho học viên, giải thích dễ hiểu, có ví dụ minh hoạ." + ParagraphStructureInstruction +
                "\n\nNội dung tài liệu:\n" + request.SourceText;
        }

        if (request.PartIndex == 0)
        {
            return $"Dựa trên nội dung sau của chương \"{request.Chapter}\" - chủ đề \"{request.Topic}\", " +
                "hãy viết PHẦN MỞ ĐẦU của một bài giảng dẫn dắt bằng tiếng Việt, giọng văn như giảng viên " +
                "đang giảng bài trực tiếp cho học viên, giải thích dễ hiểu, có ví dụ minh hoạ." + ParagraphStructureInstruction +
                " Đây là " +
                $"phần {request.PartIndex + 1}/{request.PartTotal} của tài liệu (tài liệu dài, được chia " +
                "nhỏ để giảng) — chỉ giảng đúng nội dung đoạn tài liệu dưới đây, KHÔNG kết luận/tổng kết " +
                "bài học vì còn các phần tiếp theo.\n\nNội dung tài liệu:\n" + request.SourceText;
        }

        var closingNote = request.PartIndex == request.PartTotal - 1
            ? "Đây là ĐOẠN CUỐI CÙNG của bài giảng — giảng PHẦN NỘI DUNG CÒN LẠI một cách súc tích (đừng " +
              "dàn trải), rồi BẮT BUỘC dành khoảng 80-120 từ cuối cùng để tổng kết ngắn gọn cho cả bài " +
              "(chỉ 3-5 câu) — phải viết trọn vẹn phần tổng kết này, không được để dở dang."
            : "Đây CHƯA phải đoạn cuối — KHÔNG kết luận/tổng kết, sẽ còn phần tiếp theo.";

        return $"Bạn đang giảng dở một bài giảng tiếng Việt về chương \"{request.Chapter}\" - chủ đề " +
            $"\"{request.Topic}\" cho học viên. Đây là phần {request.PartIndex + 1}/{request.PartTotal}, " +
            "TIẾP NỐI trực tiếp mạch giảng đang dở — KHÔNG mở đầu lại, KHÔNG chào hỏi lại từ đầu, KHÔNG " +
            "tóm tắt lại phần trước, KHÔNG lặp lại bất kỳ câu/ý/gạch đầu dòng nào đã xuất hiện trong " +
            "đoạn vừa giảng xong bên dưới. Nếu đoạn vừa giảng xong bị cắt dở ở giữa câu hoặc giữa một " +
            "mục liệt kê, hãy viết tiếp NGAY cho trọn câu/mục đó rồi mới chuyển sang ý mới — không viết " +
            "lại từ đầu mục đó." + ParagraphStructureInstruction + " " + closingNote +
            "\n\nĐoạn bạn vừa giảng xong (chỉ để bạn biết mình đang dừng ở đâu, TUYỆT ĐỐI không lặp lại " +
            "nội dung này):\n..." + request.PreviousTail +
            "\n\nNội dung tài liệu cho đoạn tiếp theo cần giảng:\n" + request.SourceText;
    }

    public Task<GenerateComprehensionQuestionsResponse> GenerateComprehensionQuestionsAsync(GenerateComprehensionQuestionsRequest request, CancellationToken ct) =>
        cache.GetOrCreateAsync("comprehension", $"{request.Chapter}|{request.SourceText}", async () =>
        {
            var prompt =
                $"Dựa trên nội dung chương \"{request.Chapter}\" sau đây, hãy tạo 3 câu hỏi kiểm tra " +
                "mức độ hiểu bài ngắn gọn bằng tiếng Việt. Mỗi câu hỏi một dòng, không đánh số, không " +
                "giải thích thêm.\n\nNội dung:\n" + request.SourceText;

            var raw = await aiRouter.CompleteTextAsync([new AiMessage("user", prompt)], maxTokens: 300, ct);
            var questions = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Length > 0)
                .ToList();

            return new GenerateComprehensionQuestionsResponse(questions);
        });
}
