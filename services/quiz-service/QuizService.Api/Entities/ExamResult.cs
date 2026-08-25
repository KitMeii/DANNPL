namespace QuizService.Api.Entities;

public sealed class ExamResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid UserId { get; init; }
    public decimal Score { get; set; }
    public int Correct { get; set; }
    public int Total { get; set; }
    public int TimeSpentSeconds { get; set; }

    // Việc 4.1 (2026-08-19) — true khi dòng này được tạo bởi cơ chế chống-thoát (beacon lúc rời
    // trang hoặc lazy-check sau đó), không phải học viên tự bấm Nộp. Hiển thị nhãn minh bạch ở
    // "Kết quả của tôi" và phía giáo viên xem.
    public bool IsAutoSubmitted { get; set; }
    public Guid? ExamSessionId { get; set; }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
