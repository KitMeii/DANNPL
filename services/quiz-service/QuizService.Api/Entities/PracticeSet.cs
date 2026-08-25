namespace QuizService.Api.Entities;

/// <summary>Việc 4.4 Phần B (2026-08-20) — "Đề luyện tập" giáo viên tạo sẵn từ ngân hàng câu hỏi có
/// sẵn (không sinh AI mới), giao riêng cho 1 hay nhiều Lớp. KHÁC ExamSet/ExamVersion (bộ đề THI):
/// không snapshot danh sách câu cụ thể — chỉ lưu Chapter, danh sách câu luôn tính "sống" tại thời
/// điểm học viên vào làm (qua IQuestionService.ListForPracticeAsync, y hệt luyện tập ngẫu nhiên
/// theo chương) — câu mới publish thêm vào chương tự động xuất hiện, không cần giáo viên thao tác
/// lại. Học viên làm bài qua nguyên luồng luyện tập cũ (GET .../questions/practice?chapter=X rồi
/// POST /practice/submit) — điểm luôn vào QuizResult (Luyện tập), KHÔNG lẫn sang ExamResult (Thi
/// thử) vì không đụng gì tới ExamSession/ExamVersion.</summary>
public sealed class PracticeSet
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Ten { get; set; }
    public required string Chapter { get; set; }
    public required Guid GiaoVienId { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
