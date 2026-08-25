using FluentValidation;
using QuizService.Api.Dtos;

namespace QuizService.Api.Validators;

public sealed class ScoresByUsersRequestValidator : AbstractValidator<ScoresByUsersRequest>
{
    public ScoresByUsersRequestValidator()
    {
        RuleFor(x => x.UserIds).NotEmpty();
        RuleFor(x => x.UserIds.Count).LessThanOrEqualTo(1000)
            .WithMessage("Chỉ được truy vấn tối đa 1000 người dùng mỗi lần.");
    }
}
