namespace QuizService.Api.Entities;

public sealed class EssayQuestion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Chapter { get; set; }
    public required string QuestionText { get; set; }

    /// <summary>Đáp án gợi ý cho giáo viên tự chấm — không phải chấm điểm tự động, không bao giờ
    /// trả về cho học viên (cùng nguyên tắc với OralQuestion.ExpectedAnswer).</summary>
    public string? SuggestedAnswer { get; set; }

    public Guid? CreatedBy { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>"Manual" | "Imported" | "AiGenerated".</summary>
    public string SourceType { get; set; } = "Manual";

    /// <summary>content-service Material.Id this question was generated from — soft reference (no
    /// FK, different database), null when SourceType is "Manual".</summary>
    public Guid? SourceMaterialId { get; set; }

    /// <summary>Quyết định mở rộng ngoài đặc tả C3 gốc (chỉ nêu Question) — áp cùng nguyên tắc cho
    /// EssayQuestion vì câu tự luận AI sinh (C1) có cùng rủi ro chưa qua kiểm duyệt. Xem
    /// Question.IsPublishedForPractice.</summary>
    public bool IsPublishedForPractice { get; set; }
}
