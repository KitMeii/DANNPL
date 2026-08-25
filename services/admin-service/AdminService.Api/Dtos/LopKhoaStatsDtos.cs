namespace AdminService.Api.Dtos;

/// <summary>DiemTBThiThu/DiemTBLuyenTap tách riêng "thi thử" (ExamResult) và "luyện tập"
/// (QuizResult) — 2 loại khác ý nghĩa (đã thống nhất khi audit dashboard Admin, dùng lại kết luận
/// đó ở đây), không gộp chung 1 con số. null nghĩa là chưa có học viên nào trong Lớp/Khóa có lượt
/// nào loại đó, không phải 0 điểm.</summary>
public sealed record LopDiemTrungBinhResponse(
    Guid LopId,
    string LopTen,
    int TongHocVien,
    decimal? DiemTBThiThu,
    int TongLuotThiThu,
    decimal? DiemTBLuyenTap,
    int TongLuotLuyenTap,
    IReadOnlyList<HocVienDiemResponse> HocVien);

/// <summary>Điểm của 1 học viên cụ thể trong Lớp — dùng cho dashboard Teacher (Gap 2) hiển thị
/// bảng điểm từng học viên, cùng nguyên tắc tách thi thử/luyện tập như LopDiemTrungBinhResponse.
/// Diem*/SoLuot* null/0 nghĩa là học viên đó chưa có lượt nào loại đó.</summary>
public sealed record HocVienDiemResponse(
    Guid UserId,
    string HoTen,
    string ChucVu,
    decimal? DiemThiThu,
    int SoLuotThiThu,
    decimal? DiemLuyenTap,
    int SoLuotLuyenTap);

public sealed record KhoaDiemTrungBinhResponse(
    Guid KhoaId,
    string KhoaTen,
    int TongHocVien,
    decimal? DiemTBThiThu,
    int TongLuotThiThu,
    decimal? DiemTBLuyenTap,
    int TongLuotLuyenTap);
