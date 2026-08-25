namespace AuthService.Api.Entities;

/// <summary>Rà soát Lần XVI (2026-08-21) — 1 Môn học có thể dạy cho nhiều Lớp cùng lúc (quan hệ
/// nhiều-nhiều), khóa chính ghép (MonHocId, LopId). Cùng dạng bảng nối đơn giản với
/// QuestionLopVisibility bên quiz-service, không có cột riêng nào khác.</summary>
public sealed class MonHocLop
{
    public required Guid MonHocId { get; init; }
    public required Guid LopId { get; init; }
}
