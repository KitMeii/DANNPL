using FluentValidation;
using ProgressService.Api.Dtos;

namespace ProgressService.Api.Validators;

public sealed class LogStudyTimeRequestValidator : AbstractValidator<LogStudyTimeRequest>
{
    public LogStudyTimeRequestValidator()
    {
        RuleFor(x => x.Minutes).InclusiveBetween(1, 600);
    }
}

public sealed class RecordScoreRequestValidator : AbstractValidator<RecordScoreRequest>
{
    public RecordScoreRequestValidator()
    {
        RuleFor(x => x.Score).InclusiveBetween(0, 10);
    }
}
