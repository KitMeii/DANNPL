using QuizService.Api.Dtos;

namespace QuizService.Api.Services;

/// <summary>Việc 4.4 Phần B (2026-08-20) — xem remarks Entities/PracticeSet.cs.</summary>
public interface IPracticeSetService
{
    /// <summary>Nguồn combobox "Chương X (N câu)" khi giáo viên tạo Đề luyện tập — đếm câu đã xuất
    /// bản (IsPublishedForPractice) theo Chapter, KHÔNG lọc theo Lớp cụ thể nào (góc nhìn ngân hàng
    /// chung của giáo viên).</summary>
    Task<IReadOnlyList<ChapterOptionResponse>> ListChapterOptionsAsync(CancellationToken ct);

    Task<PracticeSetResponse> CreateAsync(CreatePracticeSetRequest request, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Teacher: chỉ đề của chính mình. Admin: toàn bộ.</summary>
    Task<IReadOnlyList<PracticeSetResponse>> ListMineAsync(Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Chỉ người tạo (hoặc Admin) mới xóa được.</summary>
    Task DeleteAsync(Guid id, Guid callerUserId, string callerRole, CancellationToken ct);

    /// <summary>Học viên (hoặc GV) xem đề khả dụng cho Lớp của MÌNH — callerLopId null = không thấy
    /// đề nào (mọi PracticeSet đều bắt buộc có ≥1 Lớp, không có "toàn hệ thống" thật sự phát sinh từ
    /// UI, nhưng công thức lọc vẫn giữ đúng quy ước 0-dòng-là-toàn-hệ-thống để nhất quán kỹ thuật).</summary>
    Task<IReadOnlyList<PracticeSetResponse>> ListAvailableAsync(Guid? callerLopId, CancellationToken ct);
}
