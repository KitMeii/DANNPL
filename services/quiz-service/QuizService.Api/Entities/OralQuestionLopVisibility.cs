namespace QuizService.Api.Entities;

/// <summary>Việc 4.4 Phần A (2026-08-20) — vá gap: 3 bảng visibility (Việc 8) chưa có bản cho câu
/// hỏi Vấn đáp. Song song với <see cref="QuestionLopVisibility"/> — xem summary ở đó cho cơ chế đầy
/// đủ (0 dòng = toàn hệ thống, có dòng = chỉ Lớp đó). Áp dụng cho CẢ luyện tập vấn đáp lẫn thi vấn
/// đáp — cả 2 đều lấy pool câu hỏi qua GET /oral-questions/practice (OralQuestionService.
/// ListForPracticeAsync), không có đường lấy câu nào khác.</summary>
public sealed class OralQuestionLopVisibility
{
    public required Guid OralQuestionId { get; init; }
    public required Guid LopId { get; init; }
}
