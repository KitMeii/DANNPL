using AiService.Api.AiProviders;
using AiService.Api.Dtos;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Common;

namespace AiService.Api.Services;

public sealed class ChatService(IAiProviderRouter aiRouter, IOptions<SubjectOptions> subjectOptions) : IChatService
{
    private const int MaxHistoryMessages = 12;

    // Việc I (2026-08-20) — audit thật phát hiện câu trả lời dài bị CẮT CỤT giữa chừng ở mức 1024
    // token. Tăng lên mức đủ cho câu trả lời có cấu trúc (nhiều luận điểm + kết luận) mà không quá
    // đà — tham khảo LectureService dùng 4000 cho 1 chunk bài giảng đầy đủ; chat không cần dài bằng
    // cả bài giảng nên chọn mức trung gian.
    private const int MaxTokens = 3000;

    private string BuildSystemPrompt()
    {
        var subject = subjectOptions.Value;
        return $"Bạn là một Giảng viên Ảo chuyên về {subject.SubjectName}, hỗ trợ học viên tại " +
            $"{subject.SchoolName}. Trả lời chính xác, súc tích, bằng tiếng Việt, bám sát nội dung " +
            "học phần. Nếu câu hỏi nằm ngoài phạm vi môn học, hãy nhắc học viên quay lại chủ đề.";
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        var history = request.Messages.TakeLast(MaxHistoryMessages)
            .Select(m => new AiMessage(m.Role, m.Content));

        var messages = new List<AiMessage> { new("system", BuildSystemPrompt()) };
        messages.AddRange(history);

        // aiRouter.ChatAsync — không qua retry/failover ở giai đoạn này, xem remarks ở
        // AiProviderRouter.ChatAsync.
        var reply = await aiRouter.ChatAsync(messages, MaxTokens, ct);
        return new ChatResponse(reply);
    }
}
