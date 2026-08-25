namespace QuizService.Api.Entities;

/// <summary>Việc 8 (2026-08-16) — giới hạn phạm vi hiển thị 1 câu hỏi TN tới 1 hoặc vài Lớp cụ thể.
/// Question KHÔNG có dòng nào trong bảng này = hiện toàn hệ thống (hành vi mặc định/cũ, không đổi).
/// Question CÓ ít nhất 1 dòng = CHỈ hiện cho học viên thuộc đúng (các) LopId đó (QuestionService.
/// ListForPracticeAsync). LopId là soft-ref sang auth-service.Lop (khác database/schema, không FK
/// cứng được — cùng quy ước với Question.SourceMaterialId tham chiếu content-service.Material).</summary>
public sealed class QuestionLopVisibility
{
    public required Guid QuestionId { get; init; }
    public required Guid LopId { get; init; }
}
