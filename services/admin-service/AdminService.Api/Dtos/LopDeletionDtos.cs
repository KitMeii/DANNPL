namespace AdminService.Api.Dtos;

/// <summary>Việc 4.2 mục 3 (2026-08-19) — số liệu đếm thật (KHÔNG phải ước lượng) hiển thị ở modal
/// xác nhận TRƯỚC khi cho phép xóa, đúng yêu cầu "preview modal với số liệu THẬT".</summary>
public sealed record LopDeletionCounts(
    int StudentsCount,
    int QuizResultsCount,
    int ExamResultsCount,
    int ExamSessionsCount,
    int OralResultsCount,
    int WrongAnswersCount,
    int QuestionVisibilityCount,
    int EssayQuestionVisibilityCount,
    int ExamVersionVisibilityCount,
    int StudentProgressCount,
    int StudyLogsCount,
    int ActivityLogsCount);

/// <summary>Kết quả bước "Chuẩn bị xóa" — BackupJson là toàn văn file backup, CHỈ trả về 1 lần ở
/// bước này để trình duyệt tải về máy Admin NGAY (không lưu server, không lưu lại trong audit row).
/// Nút "Xóa vĩnh viễn" ở FE chỉ được bật SAU KHI request này thành công VÀ file đã kích hoạt tải về
/// — xem 3 bổ sung an toàn đã duyệt trong FrontEnd/admin/quan-tri-he-thong.html.</summary>
public sealed record PrepareLopDeletionResponse(
    Guid PreparationId,
    Guid LopId,
    string LopTen,
    LopDeletionCounts Counts,
    string BackupJson,
    DateTime PreparedAtUtc,
    DateTime ExpiresAtUtc);

/// <summary>ConfirmedLopTen phải khớp CHÍNH XÁC tên Lớp tại thời điểm Prepare (chống gõ nhầm/click
/// nhầm) — kiểm tra LẠI Ở SERVER, không chỉ tin FE đã khóa nút.</summary>
public sealed record ExecuteLopDeletionRequest(Guid PreparationId, string ConfirmedLopTen);

public sealed record LopDeletionStepResult(string Step, bool Success, string Detail);

public sealed record ExecuteLopDeletionResponse(
    Guid LopId,
    string Status,
    IReadOnlyList<LopDeletionStepResult> Steps,
    DateTime CompletedAtUtc);

public sealed record LopDeletionAuditResponse(
    Guid Id,
    Guid PreparationId,
    Guid AdminUserId,
    Guid LopId,
    string LopTen,
    string Status,
    LopDeletionCounts Counts,
    DateTime PreparedAtUtc,
    DateTime? CompletedAtUtc);
