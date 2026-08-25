namespace QuizService.Api.Dtos;

/// <summary>LopIds rỗng/null = mọi mã đề sinh ra "Toàn hệ thống" (mặc định, hành vi cũ) — Việc 8
/// (2026-08-16). Áp dụng chung cho MỌI ExamVersion sinh trong 1 lần gọi (1 lựa chọn phạm vi cho cả
/// lô, khớp đúng UI modal "Bộ đề mới" chỉ có 1 điểm chọn Phạm vi hiển thị).</summary>
public sealed record GenerateExamSetVersionsRequest(string Ten, List<Guid> PoolQuestionIds, Guid? MaterialId, int TargetCount, int VersionCount, List<Guid>? LopIds = null);

/// <summary>Việc 5 (2026-08-16) — "Bộ đề VĐ mới" từ ngân hàng câu hỏi vấn đáp có sẵn (không sinh AI
/// mới). Không có MaterialId vì OralQuestion không gắn nguồn tài liệu như Question.</summary>
public sealed record GenerateOralExamSetVersionsRequest(string Ten, List<Guid> PoolOralQuestionIds, int TargetCount, int VersionCount, List<Guid>? LopIds = null);

/// <summary>Việc 8 — sửa lại phạm vi hiển thị của 1 mã đề đã có. LopIds rỗng = trả về toàn hệ
/// thống.</summary>
public sealed record UpdateExamVersionLopVisibilityRequest(List<Guid> LopIds);

/// <summary>Đúng 1 trong 2 mảng có dữ liệu tùy Kind ("Mcq" → Questions, "Oral" → OralQuestions),
/// mảng còn lại rỗng — gộp chung 1 response type để "Bộ đề đã tạo" hiện được cả 2 loại trong 1
/// danh sách duy nhất (Việc 5, tránh 2 khái niệm "bộ đề" song song).</summary>
public sealed record ExamVersionResponse(Guid Id, string MaDe, bool IsPublished, string Kind, List<QuestionResponse> Questions, List<OralQuestionResponse> OralQuestions, IReadOnlyList<Guid> LopIds);

public sealed record ExamSetResponse(Guid Id, string Ten, Guid? MaterialId, int TotalPoolSize, Guid? CreatedBy, DateTime CreatedAtUtc, List<ExamVersionResponse> Versions);

public sealed record ExamSetSummaryResponse(Guid Id, string Ten, int TotalPoolSize, int VersionCount, DateTime CreatedAtUtc);

public sealed record PublishVersionResponse(int PublishedCount);

public sealed record UnpublishVersionResponse(int UnpublishedCount);
