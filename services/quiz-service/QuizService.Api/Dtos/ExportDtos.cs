namespace QuizService.Api.Dtos;

/// <summary>Trộn được MCQ (Question), tự luận (EssayQuestion) và vấn đáp (OralQuestion, thêm Việc 5)
/// trong cùng 1 lần xuất — 3 danh sách riêng vì 3 entity khác bảng, không chung không gian Guid.
/// OralQuestionIds mặc định rỗng ([]) để không phá vỡ mọi lời gọi cũ (C4) chỉ biết 2 tham số đầu.</summary>
public sealed record ExportWordRequest(List<Guid> QuestionIds, List<Guid> EssayQuestionIds, List<Guid>? OralQuestionIds = null);
