using ContentService.Api.Dtos;
using FluentValidation;

namespace ContentService.Api.Validators;

public sealed class CreateMaterialRequestValidator : AbstractValidator<CreateMaterialRequest>
{
    public CreateMaterialRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Chapter).MaximumLength(128);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(512);
        RuleFor(x => x.FileUrl).NotEmpty();
        RuleFor(x => x.FileSize).GreaterThan(0);
    }
}

public sealed class UpdateMaterialRequestValidator : AbstractValidator<UpdateMaterialRequest>
{
    public UpdateMaterialRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Chapter).MaximumLength(128);
    }
}
