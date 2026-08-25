using AdminService.Api.Dtos;
using FluentValidation;
using Shared.Contracts;

namespace AdminService.Api.Validators;

public sealed class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator()
    {
        RuleFor(x => x.Role).Must(role => Roles.All.Contains(role))
            .WithMessage($"Role phải là một trong: {string.Join(", ", Roles.All)}.");
    }
}

/// <summary>Rà soát Lần XII (2026-08-21) — cùng ràng buộc Email/Password/Name với
/// RegisterRequestValidator bên auth-service (validate lại ở đây trước khi gọi sang, tránh 1 vòng
/// round-trip lỗi 400 muộn).</summary>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Role).Must(role => Roles.All.Contains(role))
            .WithMessage($"Role phải là một trong: {string.Join(", ", Roles.All)}.");
    }
}

/// <summary>Rỗng chỉ để thỏa mãn ValidationEndpointFilter&lt;T&gt; — IsLocked là bool, không cần rule.</summary>
public sealed class SetUserLockedRequestValidator : AbstractValidator<SetUserLockedRequest>;

public sealed class SetConfigRequestValidator : AbstractValidator<SetConfigRequest>
{
    public SetConfigRequestValidator()
    {
        RuleFor(x => x.Value).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ExecuteLopDeletionRequestValidator : AbstractValidator<ExecuteLopDeletionRequest>
{
    public ExecuteLopDeletionRequestValidator()
    {
        RuleFor(x => x.PreparationId).NotEmpty();
        RuleFor(x => x.ConfirmedLopTen).NotEmpty().MaximumLength(128);
    }
}
