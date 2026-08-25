namespace QuizService.Api.Entities;

/// <summary>Việc 8 (2026-08-16) — phạm vi hiển thị của 1 mã đề (ExamVersion), chọn lúc sinh "Bộ đề
/// mới" (C2/Việc 5/Việc 7) hoặc sửa lại sau. ExamVersion không có nơi nào phục vụ trực tiếp cho học
/// viên (học viên chỉ thấy câu hỏi qua GetPracticeQuestions, lọc theo QuestionLopVisibility) — nên
/// tác dụng thực tế của bảng này là ở ExamSetService.PublishVersionAsync: khi publish 1 mã đề TN có
/// giới hạn Lớp, các câu hỏi thành viên ĐANG LÀ TOÀN HỆ THỐNG (chưa có dòng QuestionLopVisibility
/// nào) sẽ được gán đúng phạm vi Lớp của mã đề. Câu hỏi đã có phạm vi riêng (do giáo viên tự gán
/// trước đó) được giữ nguyên, không bị mã đề ghi đè — quyết định trực tiếp trên từng câu luôn ưu
/// tiên hơn hành động hàng loạt qua mã đề.</summary>
public sealed class ExamVersionLopVisibility
{
    public required Guid ExamVersionId { get; init; }
    public required Guid LopId { get; init; }
}
