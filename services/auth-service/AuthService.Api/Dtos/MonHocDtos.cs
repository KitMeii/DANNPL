namespace AuthService.Api.Dtos;

/// <summary>Rà soát Lần XVI (2026-08-21) — GiaoVienName/LopList resolve sẵn (join thẳng trong cùng
/// DB auth-service — Users/Lop/Khoa đều nằm chung schema, không cần gọi service khác) để frontend
/// không phải tự ghép nhiều lời gọi như panel "Quản lý GV" phải làm với Lớp phụ trách.</summary>
public sealed record MonHocResponse(
    Guid Id,
    string Ten,
    string MaHocPhan,
    int TinChi,
    Guid? GiaoVienId,
    string? GiaoVienName,
    IReadOnlyList<MonHocLopResponse> LopDangHoc,
    DateTime CreatedAtUtc);

public sealed record MonHocLopResponse(Guid LopId, string LopTen, Guid KhoaId, string KhoaTen);

public sealed record CreateMonHocRequest(string Ten, string MaHocPhan, int TinChi, Guid? GiaoVienId);

public sealed record UpdateMonHocRequest(string Ten, string MaHocPhan, int TinChi, Guid? GiaoVienId);

/// <summary>Thay toàn bộ danh sách Lớp đang học môn này (ghi đè, không cộng dồn) — cùng quy ước
/// UpdateQuestionLopVisibilityRequest bên quiz-service.</summary>
public sealed record AssignMonHocLopRequest(List<Guid> LopIds);
