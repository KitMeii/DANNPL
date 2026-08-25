using System.Text.RegularExpressions;
using AuthService.Api.Dtos;
using FluentValidation;
using Shared.Contracts;

namespace AuthService.Api.Validators;

/// <summary>Việc 3.1 (2026-08-19) — quy tắc dùng chung cho CapBac/SoDienThoai/NamHoc (TÙY CHỌN, mọi
/// rule chỉ áp dụng khi có giá trị — bỏ trống luôn hợp lệ). Cả RegisterRequestValidator lẫn
/// UpdateProfileRequestValidator đều cần đúng 3 rule này, tách ra đây để không lặp logic
/// BeValidNamHoc 2 lần.</summary>
internal static class OptionalPersonalFieldRules
{
    public static bool BeValidNamHoc(string? namHoc)
    {
        if (string.IsNullOrWhiteSpace(namHoc)) return true;
        var match = Regex.Match(namHoc, @"^(\d{4})-(\d{4})$");
        return match.Success && int.Parse(match.Groups[2].Value) == int.Parse(match.Groups[1].Value) + 1;
    }
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);

        RuleFor(x => x.CapBac).Must(cb => CapBacValues.All.Contains(cb)).When(x => !string.IsNullOrWhiteSpace(x.CapBac))
            .WithMessage($"Cấp bậc phải thuộc danh sách hợp lệ: {string.Join(", ", CapBacValues.All)}.");
        RuleFor(x => x.SoDienThoai).Matches(@"^0\d{9}$").When(x => !string.IsNullOrWhiteSpace(x.SoDienThoai))
            .WithMessage("Số điện thoại phải gồm đúng 10 số, bắt đầu bằng 0.");
        RuleFor(x => x.NamHoc).Must(OptionalPersonalFieldRules.BeValidNamHoc).When(x => !string.IsNullOrWhiteSpace(x.NamHoc))
            .WithMessage("Năm học phải đúng định dạng YYYY-YYYY, năm sau = năm trước + 1 (VD 2025-2026).");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator()
    {
        RuleFor(x => x.Role).Must(role => Roles.All.Contains(role))
            .WithMessage($"Role phải là một trong: {string.Join(", ", Roles.All)}.");
    }
}

/// <summary>Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp, cùng ràng buộc
/// Email/Password/Name với RegisterRequestValidator, cộng Role phải hợp lệ (cùng ChangeRoleRequestValidator).</summary>
public sealed class CreateUserByAdminRequestValidator : AbstractValidator<CreateUserByAdminRequest>
{
    public CreateUserByAdminRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Role).Must(role => Roles.All.Contains(role))
            .WithMessage($"Role phải là một trong: {string.Join(", ", Roles.All)}.");
    }
}

/// <summary>Rỗng chỉ để thỏa mãn ValidationEndpointFilter&lt;T&gt; (yêu cầu IValidator&lt;T&gt;
/// đăng ký) — IsLocked là bool, JSON deserialization đã đảm bảo hợp lệ, không cần rule.</summary>
public sealed class SetUserLockedRequestValidator : AbstractValidator<SetUserLockedRequest>;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);

        RuleFor(x => x.CapBac).Must(cb => CapBacValues.All.Contains(cb)).When(x => !string.IsNullOrWhiteSpace(x.CapBac))
            .WithMessage($"Cấp bậc phải thuộc danh sách hợp lệ: {string.Join(", ", CapBacValues.All)}.");
        RuleFor(x => x.SoDienThoai).Matches(@"^0\d{9}$").When(x => !string.IsNullOrWhiteSpace(x.SoDienThoai))
            .WithMessage("Số điện thoại phải gồm đúng 10 số, bắt đầu bằng 0.");
        RuleFor(x => x.NamHoc).Must(OptionalPersonalFieldRules.BeValidNamHoc).When(x => !string.IsNullOrWhiteSpace(x.NamHoc))
            .WithMessage("Năm học phải đúng định dạng YYYY-YYYY, năm sau = năm trước + 1 (VD 2025-2026).");
        RuleFor(x => x.BoMonKhoa).MaximumLength(200);
        RuleFor(x => x.ChucVuGV).Must(cv => ChucVuGvValues.All.Contains(cv)).When(x => !string.IsNullOrWhiteSpace(x.ChucVuGV))
            .WithMessage($"Chức vụ phải thuộc danh sách hợp lệ: {string.Join(", ", ChucVuGvValues.All)}.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
