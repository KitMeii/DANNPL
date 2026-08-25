namespace AuthService.Api.Entities;

/// <summary>Chức vụ của học viên trong lớp — thuần hiển thị/tổ chức, không phải cơ chế phân quyền
/// (đó là Role trong Shared.Contracts.Roles). Chỉ Admin hoặc GV chủ nhiệm của đúng lớp đó được sửa.</summary>
public static class ChucVuValues
{
    public const string HocVien = "Học viên";
    public const string LopTruong = "Lớp trưởng";
    public const string LopPho = "Lớp phó";

    public static readonly string[] All = [HocVien, LopTruong, LopPho];
}
