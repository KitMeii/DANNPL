namespace QuizService.Api.Entities;

/// <summary>1 mã đề cụ thể (vd "101") thuộc 1 ExamSet — câu hỏi thật nằm ở bảng nối
/// ExamVersionQuestion.</summary>
public sealed class ExamVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ExamSetId { get; init; }
    public required string MaDe { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>"Mcq" (mặc định, câu hỏi ở ExamVersionQuestion→Question) hoặc "Oral" (câu hỏi ở
    /// ExamVersionOralQuestion→OralQuestion) — thêm ở Việc 5 (2026-08-16) để dùng chung 1 khái
    /// niệm ExamSet/ExamVersion ("Bộ đề") cho cả 2 loại thay vì 2 khái niệm song song, dù OralQuestion
    /// không FK chung được với Question (ExamVersionQuestion.QuestionId có FK cứng tới Question).
    /// OralQuestion không có publish-gate (audit trước xác nhận cố ý) nên version Kind=Oral không
    /// dùng Publish/UnpublishVersionAsync — 2 hàm đó chặn tường minh nếu gọi nhầm trên version Oral.</summary>
    public string Kind { get; set; } = "Mcq";

    /// <summary>Thêm sau audit "Việc 1" (rủi ro cascade-delete): đánh dấu mã đề này đã được giáo
    /// viên chủ động xuất bản (PublishVersionAsync) hay chưa. Cần field này để QuestionService biết
    /// chính xác "câu hỏi này có thuộc 1 mã đề ĐÃ xuất bản không" khi chặn xóa — trước đó không có
    /// state riêng ở ExamVersion, chỉ suy ra qua Question.IsPublished (không đủ chính
    /// xác vì field đó có thể true do lý do khác, không liên quan mã đề này).</summary>
    public bool IsPublished { get; set; }
}
