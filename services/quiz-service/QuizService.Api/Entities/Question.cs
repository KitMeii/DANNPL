namespace QuizService.Api.Entities;

public sealed class Question
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Chapter { get; set; }
    public required string QuestionText { get; set; }
    public required string OptionA { get; set; }
    public required string OptionB { get; set; }
    public required string OptionC { get; set; }
    public required string OptionD { get; set; }

    /// <summary>0=A, 1=B, 2=C, 3=D. Never serialized to a student-facing response before grading.</summary>
    public required int CorrectAnswer { get; set; }

    public string? Explanation { get; set; }
    public Guid? CreatedBy { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>"Manual" | "Imported" | "AiGenerated". Default "Manual" covers every question
    /// created before this field existed and any teacher-typed entry going forward.</summary>
    public string SourceType { get; set; } = "Manual";

    /// <summary>content-service Material.Id this question was generated/imported from — soft
    /// reference (no FK, different database), null when SourceType is "Manual".</summary>
    public Guid? SourceMaterialId { get; set; }

    /// <summary>1=dễ, 2=trung bình, 3=khó. Null = chưa phân loại (câu hỏi cũ trước khi field này
    /// tồn tại) — thuật toán chọn đề (ExamSetGenerationService) coi null như 2 khi cân đối.</summary>
    public int? Difficulty { get; set; }

    public string? Topic { get; set; }

    /// <summary>Chỉ câu hỏi đã xuất bản mới xuất hiện ở luyện tập học viên (ListForPracticeAsync).
    /// Manual (giáo viên tự soạn) mặc định true — giữ nguyên trải nghiệm cũ. AiGenerated/Imported
    /// mặc định false — cần giáo viên chủ động bấm "Xuất bản" sau khi kiểm duyệt (C3), tránh câu
    /// hỏi AI sinh nhưng chưa qua kiểm chất lượng vô tình lộ ra cho học viên làm bài.</summary>
    public bool IsPublishedForPractice { get; set; }
}
