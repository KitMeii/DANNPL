namespace QuizService.Api.Dtos;

/// <summary>Việc 4.4 Phần B (2026-08-20) — LopIds BẮT BUỘC ≥1 (validate ở
/// CreatePracticeSetRequestValidator, không ở DB — xem remarks PracticeSetLopVisibility.cs).</summary>
public sealed record CreatePracticeSetRequest(string Ten, string Chapter, List<Guid> LopIds);

/// <summary>QuestionCount tính "sống" tại thời điểm gọi (không snapshot) — số câu
/// IsPublishedForPractice=true thuộc đúng Chapter. Ở ListMineAsync (GV xem đề mình tạo): đếm theo
/// góc nhìn ngân hàng chung (không lọc theo Lớp cụ thể nào). Ở ListAvailableAsync (học viên xem đề
/// khả dụng): đếm đúng những câu HỌC VIÊN ĐÓ sẽ thấy (đã lọc theo callerLopId của họ).</summary>
public sealed record PracticeSetResponse(Guid Id, string Ten, string Chapter, Guid GiaoVienId, DateTime CreatedAtUtc, IReadOnlyList<Guid> LopIds, int QuestionCount);

/// <summary>1 chương trong ngân hàng kèm số câu đã xuất bản — nguồn cho combobox "Chương 1 (15 câu)"
/// khi giáo viên tạo Đề luyện tập.</summary>
public sealed record ChapterOptionResponse(string Chapter, int PublishedQuestionCount);
