namespace AiService.Api.Dtos;

public sealed record ExtractQuestionsRequest(string Chapter, string SourceText, int Count = 10);

/// <summary>Difficulty/Topic chỉ được AI gán khi trích xuất bộ đề lớn (C2, ExtractAsync) — sinh đề
/// đơn lẻ (C1, GenerateExamSetAsync) không yêu cầu 2 field này nên luôn null ở đường đó.</summary>
public sealed record ExtractedQuestion(string QuestionText, string OptionA, string OptionB, string OptionC, string OptionD, int CorrectAnswer, string? Explanation, int? Difficulty = null, string? Topic = null);

public sealed record ExtractQuestionsResponse(List<ExtractedQuestion> Questions);

/// <summary>Sinh một bộ đề hoàn chỉnh (trắc nghiệm + tự luận) từ nội dung tài liệu trong một lần
/// gọi LLM duy nhất — dùng cho luồng "Sinh đề bằng AI" gắn theo từng Material ở Teacher dashboard.</summary>
public sealed record GenerateExamSetRequest(string Chapter, string SourceText, int McqCount = 12, int EssayCount = 1);

public sealed record GeneratedEssayQuestion(string QuestionText, string? SuggestedAnswer);

public sealed record GenerateExamSetResponse(List<ExtractedQuestion> McqQuestions, List<GeneratedEssayQuestion> EssayQuestions);

/// <summary>"Kiểm tra nhanh kiến thức" (Student, giang-bai.html) — audit 2026-08-16 mục 3. Khác hẳn
/// ExtractQuestionsRequest (C2, Teacher/Admin-only, ghi thẳng vào pool câu hỏi chính thức):
/// endpoint này KHÔNG lưu DB, chỉ trả JSON tạm để học viên tự làm thử và chấm điểm ngay trên
/// trình duyệt — không có sourceType/publish-gate nào liên quan vì không có gì được lưu.
/// MaterialId chỉ để làm khoá cache/log, không bắt buộc (học viên có thể tự upload PDF, không có
/// Material nào đứng sau).</summary>
public sealed record QuickCheckRequest(Guid? MaterialId, string Chapter, string SourceText);

public sealed record QuickCheckResponse(List<ExtractedQuestion> Questions);
