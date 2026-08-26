namespace AdminService.Api.Dtos;

/// <summary>DiemTBThiThu = điểm TB Kiểm tra (ExamResult). null nghĩa là chưa có học viên nào trong
/// Lớp/Khóa có lượt nào, không phải 0 điểm. (Trước có thêm cặp DiemTBLuyenTap song song — bỏ cùng
/// lúc xóa tính năng Luyện tập.)</summary>
public sealed record LopDiemTrungBinhResponse(
    Guid LopId,
    string LopTen,
    int TongHocVien,
    decimal? DiemTBThiThu,
    int TongLuotThiThu,
    IReadOnlyList<HocVienDiemResponse> HocVien);

/// <summary>Điểm của 1 học viên cụ thể trong Lớp — dùng cho dashboard Teacher (Gap 2) hiển thị
/// bảng điểm từng học viên. DiemThiThu/SoLuotThiThu null/0 nghĩa là học viên đó chưa có lượt Kiểm
/// tra nào.</summary>
public sealed record HocVienDiemResponse(
    Guid UserId,
    string HoTen,
    string ChucVu,
    decimal? DiemThiThu,
    int SoLuotThiThu);

public sealed record KhoaDiemTrungBinhResponse(
    Guid KhoaId,
    string KhoaTen,
    int TongHocVien,
    decimal? DiemTBThiThu,
    int TongLuotThiThu);
