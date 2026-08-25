using AuthService.Api.Dtos;
using FluentValidation;

namespace AuthService.Api.Validators;

public sealed class CreateMonHocRequestValidator : AbstractValidator<CreateMonHocRequest>
{
    public CreateMonHocRequestValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(256);
        RuleFor(x => x.MaHocPhan).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TinChi).InclusiveBetween(1, 10);
    }
}

public sealed class UpdateMonHocRequestValidator : AbstractValidator<UpdateMonHocRequest>
{
    public UpdateMonHocRequestValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(256);
        RuleFor(x => x.MaHocPhan).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TinChi).InclusiveBetween(1, 10);
    }
}

// AssignMonHocLopRequest không có ràng buộc định dạng nào ngoài Guid hợp lệ (đã được JSON
// deserialization đảm bảo) — validator rỗng chỉ để thỏa mãn ValidationEndpointFilter<T>, cùng quy
// ước AssignLopRequestValidator ở KhoaLopValidators.cs.
public sealed class AssignMonHocLopRequestValidator : AbstractValidator<AssignMonHocLopRequest>;
