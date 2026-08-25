namespace AdminService.Api.Entities;

/// <summary>Việc 4.2 mục 3 (2026-08-19) — nhật ký "xóa toàn bộ dữ liệu Lớp" (hành động hủy diệt,
/// không khôi phục). KHÁC RoleChangeAudit (đổi Role hệ thống) — schema đó không có chỗ cho số liệu
/// đếm/kết quả từng bước saga. Bản ghi này KHÔNG nằm trong phạm vi bị xóa bởi chính hành động nó ghi
/// lại (AdminDbContext là DB riêng của admin-service, độc lập auth-service.Lop) — thỏa yêu cầu
/// "audit log không được xóa cùng dữ liệu bị xóa". KHÔNG lưu BackupJson ở đây (backup chỉ tải về máy
/// Admin, không lưu server — quyết định đã duyệt), chỉ lưu số liệu đếm để đối chiếu.</summary>
public sealed class LopDeletionAudit
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid PreparationId { get; init; }
    public required Guid AdminUserId { get; init; }
    public required Guid LopId { get; init; }
    public required string LopTen { get; init; }

    /// <summary>Prepared: đã dựng xong backup, chưa xóa gì. Completed: cả 3 bước saga đã xóa xong.
    /// PartialFailure: 1+ bước saga thất bại, xem StepResultsJson để biết bước nào — có thể retry
    /// an toàn (mọi thao tác xóa đều idempotent).</summary>
    public required string Status { get; set; }

    public int StudentsCount { get; init; }
    public int QuizResultsCount { get; init; }
    public int ExamResultsCount { get; init; }
    public int ExamSessionsCount { get; init; }
    public int OralResultsCount { get; init; }
    public int WrongAnswersCount { get; init; }
    public int QuestionVisibilityCount { get; init; }
    public int EssayQuestionVisibilityCount { get; init; }
    public int ExamVersionVisibilityCount { get; init; }
    public int StudentProgressCount { get; init; }
    public int StudyLogsCount { get; init; }
    public int ActivityLogsCount { get; init; }

    /// <summary>JSON mảng {Step, Success, Detail} — cập nhật khi ExecuteAsync chạy, null khi còn
    /// ở trạng thái Prepared (chưa xóa gì).</summary>
    public string? StepResultsJson { get; set; }

    public DateTime PreparedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}

public static class LopDeletionAuditStatus
{
    public const string Prepared = "Prepared";
    public const string Completed = "Completed";
    public const string PartialFailure = "PartialFailure";
}
