using AuthService.Api.Dtos;
using AuthService.Api.Entities;
using FluentValidation;
using Shared.Contracts;

namespace AuthService.Api.Validators;

public sealed class CreateKhoaRequestValidator : AbstractValidator<CreateKhoaRequest>
{
    public CreateKhoaRequestValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(128);
    }
}

public sealed class UpdateKhoaRequestValidator : AbstractValidator<UpdateKhoaRequest>
{
    public UpdateKhoaRequestValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(128);
    }
}

public sealed class CreateLopRequestValidator : AbstractValidator<CreateLopRequest>
{
    public CreateLopRequestValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(128);
        RuleFor(x => x.KhoaId).NotEmpty();
        RuleFor(x => x.NamHoc).MaximumLength(32);
    }
}

public sealed class UpdateLopRequestValidator : AbstractValidator<UpdateLopRequest>
{
    public UpdateLopRequestValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NamHoc).MaximumLength(32);
        // KhoaId == null nghĩa là "không đổi" (không validate); nếu caller CÓ gửi thì phải khác
        // Guid.Empty — cùng quy ước NotEmpty ở CreateLopRequestValidator.
        RuleFor(x => x.KhoaId).NotEqual(Guid.Empty).When(x => x.KhoaId.HasValue);
    }
}

// AssignLopRequest/AssignGiaoVienRequest không có ràng buộc định dạng nào ngoài Guid hợp lệ (đã
// được JSON deserialization đảm bảo) — validator rỗng chỉ để thỏa mãn AddValidatorsFromAssembly/
// ValidationEndpointFilter<T> (yêu cầu IValidator<T> đăng ký trong DI), theo đúng quy ước "mỗi
// request DTO có 1 Validator tương ứng" của dự án.
public sealed class AssignLopRequestValidator : AbstractValidator<AssignLopRequest>;

public sealed class AssignGiaoVienRequestValidator : AbstractValidator<AssignGiaoVienRequest>;

public sealed class ChangeChucVuRequestValidator : AbstractValidator<ChangeChucVuRequest>
{
    public ChangeChucVuRequestValidator()
    {
        RuleFor(x => x.ChucVu).Must(cv => ChucVuValues.All.Contains(cv))
            .WithMessage($"Chức vụ phải là một trong: {string.Join(", ", ChucVuValues.All)}.");
    }
}

public sealed class ChangeCapBacRequestValidator : AbstractValidator<ChangeCapBacRequest>
{
    public ChangeCapBacRequestValidator()
    {
        RuleFor(x => x.CapBac).Must(cb => CapBacValues.All.Contains(cb)).When(x => !string.IsNullOrWhiteSpace(x.CapBac))
            .WithMessage($"Cấp bậc phải thuộc danh sách hợp lệ: {string.Join(", ", CapBacValues.All)}.");
    }
}

// Rà soát Lần VI (2026-08-21) — không có ràng buộc định dạng nào (text tự do, Admin chỉ định) —
// validator rỗng chỉ để thỏa mãn AddValidatorsFromAssembly/ValidationEndpointFilter<T>, cùng quy
// ước AssignLopRequestValidator/AssignGiaoVienRequestValidator ở trên.
public sealed class ChangeMonHocPhuTrachRequestValidator : AbstractValidator<ChangeMonHocPhuTrachRequest>;

/// <summary>Rà soát Lần XIV (2026-08-21) — cùng rule ChucVuGV với UpdateProfileRequestValidator
/// (đường tự sửa của GV), chỉ khác đây là đường Admin sửa cho người khác.</summary>
public sealed class ChangeChucVuGvRequestValidator : AbstractValidator<ChangeChucVuGvRequest>
{
    public ChangeChucVuGvRequestValidator()
    {
        RuleFor(x => x.ChucVuGV).Must(cv => ChucVuGvValues.All.Contains(cv)).When(x => !string.IsNullOrWhiteSpace(x.ChucVuGV))
            .WithMessage($"Chức vụ phải thuộc danh sách hợp lệ: {string.Join(", ", ChucVuGvValues.All)}.");
    }
}

/// <summary>Rà soát Lần XVIII (2026-08-22) — cùng ràng buộc Name với RegisterRequestValidator,
/// NamHoc cùng rule BeValidNamHoc dùng chung (OptionalPersonalFieldRules).</summary>
public sealed class AdminEditUserRequestValidator : AbstractValidator<AdminEditUserRequest>
{
    public AdminEditUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NamHoc).Must(OptionalPersonalFieldRules.BeValidNamHoc).When(x => !string.IsNullOrWhiteSpace(x.NamHoc))
            .WithMessage("Năm học phải đúng định dạng YYYY-YYYY, năm sau = năm trước + 1 (VD 2025-2026).");
    }
}
