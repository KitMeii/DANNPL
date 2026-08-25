namespace QuizService.Api.Entities;

/// <summary>Một lần "trích xuất bộ đề lớn" (C2) — chứa N ExamVersion (mã đề), mỗi mã đề là 1 tập
/// con câu hỏi lấy từ cùng pool (vd 50/150 câu).</summary>
public sealed class ExamSet
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Ten { get; set; }

    /// <summary>content-service Material.Id nếu pool được trích từ 1 tài liệu cụ thể — soft
    /// reference (không FK, khác database), null nếu pool ghép từ nhiều nguồn/nhập tay.</summary>
    public Guid? MaterialId { get; set; }

    public int TotalPoolSize { get; set; }
    public Guid? CreatedBy { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
