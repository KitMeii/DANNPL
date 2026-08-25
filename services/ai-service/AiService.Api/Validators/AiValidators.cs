using AiService.Api.Dtos;
using FluentValidation;

namespace AiService.Api.Validators;

public sealed class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Messages).NotEmpty();
        RuleForEach(x => x.Messages).ChildRules(message =>
        {
            message.RuleFor(m => m.Role).Must(role => role is "user" or "assistant")
                .WithMessage("Role phải là 'user' hoặc 'assistant' — không được ghi đè system prompt.");
            message.RuleFor(m => m.Content).NotEmpty().MaximumLength(8000);
        });
    }
}

public sealed class GenerateLectureRequestValidator : AbstractValidator<GenerateLectureRequest>
{
    public GenerateLectureRequestValidator()
    {
        RuleFor(x => x.Chapter).NotEmpty();
        RuleFor(x => x.Topic).NotEmpty();
        // 6.000 ký tự = kích thước 1 chunk (xem LectureService.BuildPrompt) — cỡ an toàn dưới
        // trần 8.000 token/phút của Groq kể cả khi cộng thêm phần chỉ dẫn nối mạch + previousTail.
        RuleFor(x => x.SourceText).NotEmpty().MaximumLength(6000);
        RuleFor(x => x.PartTotal).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PartIndex).GreaterThanOrEqualTo(0).LessThan(x => x.PartTotal);
        RuleFor(x => x.PreviousTail).MaximumLength(800);
    }
}

public sealed class GenerateComprehensionQuestionsRequestValidator : AbstractValidator<GenerateComprehensionQuestionsRequest>
{
    public GenerateComprehensionQuestionsRequestValidator()
    {
        RuleFor(x => x.Chapter).NotEmpty();
        RuleFor(x => x.SourceText).NotEmpty().MaximumLength(20000);
    }
}

public sealed class GradeOralRequestValidator : AbstractValidator<GradeOralRequest>
{
    public GradeOralRequestValidator()
    {
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ExpectedAnswer).MaximumLength(4000);
        RuleFor(x => x.MainAnswer).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.FollowupAnswers).Must(a => a.Count <= 10)
            .WithMessage("Tối đa 10 câu trả lời bổ sung.");
        RuleForEach(x => x.FollowupAnswers).MaximumLength(4000);
    }
}

public sealed class ExtractQuestionsRequestValidator : AbstractValidator<ExtractQuestionsRequest>
{
    public ExtractQuestionsRequestValidator()
    {
        RuleFor(x => x.Chapter).NotEmpty();
        RuleFor(x => x.SourceText).NotEmpty().MaximumLength(50000);
        RuleFor(x => x.Count).InclusiveBetween(1, 30);
    }
}

public sealed class GenerateExamSetRequestValidator : AbstractValidator<GenerateExamSetRequest>
{
    public GenerateExamSetRequestValidator()
    {
        RuleFor(x => x.Chapter).NotEmpty();
        RuleFor(x => x.SourceText).NotEmpty().MaximumLength(50000);
        // 0 = không sinh loại câu hỏi đó (audit 2026-08-16 việc 3) — nhưng phải còn ít nhất 1 loại.
        RuleFor(x => x.McqCount).Must(c => c == 0 || (c >= 10 && c <= 15))
            .WithMessage("Số câu trắc nghiệm phải là 0 hoặc từ 10 đến 15.");
        RuleFor(x => x.EssayCount).Must(c => c == 0 || (c >= 1 && c <= 2))
            .WithMessage("Số câu tự luận phải là 0 hoặc từ 1 đến 2.");
        RuleFor(x => x).Must(x => x.McqCount > 0 || x.EssayCount > 0)
            .WithMessage("Cần ít nhất 1 loại câu hỏi (trắc nghiệm hoặc tự luận).");
    }
}

public sealed class QuickCheckRequestValidator : AbstractValidator<QuickCheckRequest>
{
    public QuickCheckRequestValidator()
    {
        RuleFor(x => x.Chapter).NotEmpty();
        RuleFor(x => x.SourceText).NotEmpty().MaximumLength(20000);
    }
}
