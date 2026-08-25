namespace QuizService.Api.Entities;

/// <summary>"Trắc nghiệm" (batch-submit-at-end) vs "Vấn đáp" (per-question, graded as answered) —
/// the two exam modes in thi-thu.html. A session tracks the FULL set of question IDs assigned at
/// start time, which is what lets auto-submit-on-abandon (Việc 4.1) score against the true
/// denominator instead of just what was answered.</summary>
public static class ExamSessionKind
{
    public const string TracNghiem = "TracNghiem";
    public const string VanDap = "VanDap";

    public static readonly string[] All = [TracNghiem, VanDap];
}

public static class ExamSessionStatus
{
    /// <summary>Started, not yet finalized either way.</summary>
    public const string InProgress = "InProgress";
    /// <summary>Student explicitly submitted (manually, or answered every VanDap question).</summary>
    public const string Submitted = "Submitted";
    /// <summary>Finalized by the chống-thoát mechanism (beacon at exit, or lazy-check on a later
    /// request) instead of an explicit submit — see
    /// QuizAttemptService.AutoSubmitAbandonedSessionAsync remarks.</summary>
    public const string AutoSubmittedAbandoned = "AutoSubmittedAbandoned";
}

/// <summary>
/// Việc 4.1 (2026-08-19) — trước đây quiz-service không biết 1 lượt thi "Thi thử" tồn tại cho tới
/// tận lúc submit thành công; học viên đóng tab/thoát giữa chừng thì không để lại dấu vết gì, có
/// thể chọn lọc chỉ nộp những lần làm tốt (làm lệch bảng xếp hạng). Session này là điểm neo: server
/// biết bài thi ĐÃ BẮT ĐẦU, biết chính xác bộ câu hỏi được giao (denominator thật), và có nơi để
/// gắn kết quả dù học viên có nộp tay hay không.
///
/// Bất biến quan trọng nhất: MỖI (UserId, Kind) chỉ có tối đa 1 session Status=InProgress tại 1
/// thời điểm — StartSessionAsync LUÔN chốt (auto-submit 0 điểm cho câu chưa nộp) bất kỳ session
/// InProgress cũ nào của đúng user+kind đó TRƯỚC KHI tạo session mới. Đây là cơ chế chặn đúng lỗ
/// hổng audit đã chỉ ra: không thể mở nhiều phiên song song rồi chỉ nộp phiên tốt — mở phiên mới
/// tự động "tốn" phiên cũ.
/// </summary>
public sealed class ExamSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid UserId { get; init; }
    public required string Kind { get; init; }

    /// <summary>JSON-encoded List&lt;Guid&gt; — toàn bộ câu hỏi được giao lúc bắt đầu, do CLIENT tự
    /// báo cáo (từ đúng bộ câu hỏi vừa fetch để hiển thị). Đây KHÔNG phải biên bảo mật chấm điểm
    /// (điểm số luôn tính từ đáp án đúng lưu server, xem GradeAsync/F3) — chỉ ảnh hưởng mẫu số khi
    /// TỰ ĐỘNG nộp do bỏ dở; học viên tự khai gian bộ câu hỏi của MÌNH chỉ có thể tự làm lợi cho
    /// chính kịch bản đang bị ngăn chặn (bỏ dở), rủi ro thấp và đã được cân nhắc có chủ đích.</summary>
    public required string QuestionIdsJson { get; init; }

    public required int ExpectedDurationSeconds { get; init; }
    // Không init-only — bài test giả lập "đã lâu không quay lại" bằng cách tua ngược giá trị này.
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = ExamSessionStatus.InProgress;

    /// <summary>Chỉ có giá trị khi Kind=TracNghiem (1 ExamResult/session). VanDap không có — mỗi
    /// câu vấn đáp đã là 1 OralResult riêng, xem OralResult.ExamSessionId.</summary>
    public Guid? ExamResultId { get; set; }

    /// <summary>Concurrency token — bảo đảm idempotent khi beacon (thoát trang) và lazy-check (lần
    /// gọi sau) cùng cố chốt 1 session: request nào ghi trước thắng, request sau đọc lại thấy
    /// Status đã đổi thì bỏ qua thay vì tạo ExamResult trùng (xem AutoSubmitAbandonedSessionAsync).</summary>
    public byte[]? RowVersion { get; set; }
}
