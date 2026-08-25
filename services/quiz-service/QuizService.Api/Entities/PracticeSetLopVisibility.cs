namespace QuizService.Api.Entities;

/// <summary>Việc 4.4 Phần B (2026-08-20) — bảng thứ 5 theo đúng pattern 4 bảng visibility kia (0
/// dòng = toàn hệ thống, kỹ thuật nhất quán). Nghiệp vụ: form tạo "Đề luyện tập" BẮT BUỘC chọn ≥1
/// Lớp (validate ở CreatePracticeSetRequestValidator, KHÔNG ở DB) — mục đích tính năng là giao đề
/// riêng theo lớp, đề "toàn hệ thống" trùng với luyện tập ngẫu nhiên/theo chương đã có sẵn.</summary>
public sealed class PracticeSetLopVisibility
{
    public required Guid PracticeSetId { get; init; }
    public required Guid LopId { get; init; }
}
