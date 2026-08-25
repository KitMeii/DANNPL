namespace QuizService.Api.Entities;

/// <summary>Bảng nối N-N giữa ExamVersion (Kind=Oral) và OralQuestion — song song với
/// ExamVersionQuestion (Kind=Mcq) vì OralQuestion không cùng bảng/FK-space với Question. Thêm ở
/// Việc 5 (2026-08-16) — "Bộ đề VĐ mới" từ ngân hàng có sẵn.</summary>
public sealed class ExamVersionOralQuestion
{
    public required Guid ExamVersionId { get; init; }
    public required Guid OralQuestionId { get; init; }
    public int ThuTu { get; set; }
}
