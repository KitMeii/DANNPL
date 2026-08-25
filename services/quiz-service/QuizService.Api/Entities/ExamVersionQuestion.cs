namespace QuizService.Api.Entities;

/// <summary>Bảng nối N-N giữa ExamVersion (mã đề) và Question (câu hỏi MCQ) — ThuTu giữ đúng thứ
/// tự câu hỏi trong mã đề đó (khác nhau giữa các mã, do thuật toán chọn ngẫu nhiên theo seed).</summary>
public sealed class ExamVersionQuestion
{
    public required Guid ExamVersionId { get; init; }
    public required Guid QuestionId { get; init; }
    public int ThuTu { get; set; }
}
