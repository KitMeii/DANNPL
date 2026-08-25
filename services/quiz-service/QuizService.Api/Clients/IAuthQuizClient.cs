namespace QuizService.Api.Clients;

// Việc IV (2026-08-20, rà soát Lần II mục 1.3) — thêm ChucVu để Bảng xếp hạng hiện được badge Chức
// vụ (trước chỉ Id+Name). Rà soát Lần III (2026-08-21, mục C) — thêm CapBac cùng lý do. Rà soát Lần
// V — thêm AvatarUrl để hiện ảnh đại diện thật thay vì chữ cái đầu tên.
public sealed record RemoteHocVien(Guid Id, string Name, string ChucVu, string? CapBac, string? AvatarUrl);

/// <summary>quiz-service không lưu Lớp/GiaoVienId nào — mọi thứ tra cứu qua auth-service, chuyển
/// tiếp JWT của người gọi hiện tại (không cần X-Internal-Key vì /auth/me và /auth/lop/mine chỉ
/// RequireAuthorization() thường, tự suy caller từ JWT, xem KhoaLopEndpoints.cs). Việc 8
/// (2026-08-16) — nền tảng cho giới hạn phạm vi câu hỏi/bộ đề theo Lớp.</summary>
public interface IAuthQuizClient
{
    /// <summary>LopId của chính người gọi (null nếu chưa được gán Lớp nào) — dùng để lọc
    /// GetPracticeQuestions cho học viên.</summary>
    Task<Guid?> GetMyLopIdAsync(CancellationToken ct);

    /// <summary>(Các) Lớp mà chính người gọi là GiaoVienId (chủ nhiệm) — dùng để kiểm tra giáo viên
    /// chỉ được gán phạm vi hiển thị tới Lớp mình phụ trách, không phải Lớp bất kỳ.</summary>
    Task<IReadOnlyList<Guid>> ListMyLopIdsAsync(CancellationToken ct);

    /// <summary>Việc C (2026-08-16) — roster tối thiểu (Id/Name) của 1 Lớp, dùng cho bảng xếp hạng
    /// theo Lớp. Gọi endpoint service-to-service (GET /auth/lop/{id}/hoc-vien-ids, internal-key) —
    /// KHÁC GetMyLopIdAsync/ListMyLopIdsAsync ở trên vì auth-service không tự kiểm ownership ở
    /// endpoint này, quiz-service PHẢI tự xác thực callerRole/lopId khớp nhau TRƯỚC khi gọi
    /// (QuizStatsService.GetLopLeaderboardAsync).</summary>
    Task<IReadOnlyList<RemoteHocVien>> ListHocVienAsync(Guid lopId, CancellationToken ct);
}
