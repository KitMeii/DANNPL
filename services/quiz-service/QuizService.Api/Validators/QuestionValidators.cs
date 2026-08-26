using FluentValidation;
using QuizService.Api.Dtos;

namespace QuizService.Api.Validators;

public sealed class CreateQuestionRequestValidator : AbstractValidator<CreateQuestionRequest>
{
    public CreateQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.OptionA).NotEmpty().MaximumLength(500);
        RuleFor(x => x.OptionB).NotEmpty().MaximumLength(500);
        RuleFor(x => x.OptionC).NotEmpty().MaximumLength(500);
        RuleFor(x => x.OptionD).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Explanation).MaximumLength(2000);
        RuleFor(x => x.CorrectAnswer).InclusiveBetween(0, 3);
        RuleFor(x => x.SourceType).Must(s => s is "Manual" or "Imported" or "AiGenerated")
            .WithMessage("SourceType phải là Manual, Imported hoặc AiGenerated.");
        RuleFor(x => x.Difficulty).InclusiveBetween(1, 3).When(x => x.Difficulty.HasValue);
        RuleFor(x => x.Topic).MaximumLength(128);
    }
}

public sealed class UpdateQuestionRequestValidator : AbstractValidator<UpdateQuestionRequest>
{
    public UpdateQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.OptionA).NotEmpty().MaximumLength(500);
        RuleFor(x => x.OptionB).NotEmpty().MaximumLength(500);
        RuleFor(x => x.OptionC).NotEmpty().MaximumLength(500);
        RuleFor(x => x.OptionD).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Explanation).MaximumLength(2000);
        RuleFor(x => x.CorrectAnswer).InclusiveBetween(0, 3);
    }
}

public sealed class UpdateQuestionLopVisibilityRequestValidator : AbstractValidator<UpdateQuestionLopVisibilityRequest>
{
    public UpdateQuestionLopVisibilityRequestValidator()
    {
        RuleFor(x => x.LopIds).NotNull();
    }
}

public sealed class CreateEssayQuestionRequestValidator : AbstractValidator<CreateEssayQuestionRequest>
{
    public CreateEssayQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.SuggestedAnswer).MaximumLength(4000);
        RuleFor(x => x.SourceType).Must(s => s is "Manual" or "Imported" or "AiGenerated")
            .WithMessage("SourceType phải là Manual, Imported hoặc AiGenerated.");
    }
}

public sealed class UpdateEssayQuestionRequestValidator : AbstractValidator<UpdateEssayQuestionRequest>
{
    public UpdateEssayQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.SuggestedAnswer).MaximumLength(4000);
    }
}

public sealed class UpdateEssayQuestionLopVisibilityRequestValidator : AbstractValidator<UpdateEssayQuestionLopVisibilityRequest>
{
    public UpdateEssayQuestionLopVisibilityRequestValidator()
    {
        RuleFor(x => x.LopIds).NotNull();
    }
}

public sealed class GenerateExamSetVersionsRequestValidator : AbstractValidator<GenerateExamSetVersionsRequest>
{
    public GenerateExamSetVersionsRequestValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PoolQuestionIds).NotEmpty();
        RuleFor(x => x.TargetCount).GreaterThan(0);
        RuleFor(x => x.VersionCount).InclusiveBetween(2, 4);
    }
}

/// <summary>Việc 5 (2026-08-16) — "Bộ đề VĐ mới" từ ngân hàng có sẵn. TargetCount 1-4 theo đúng
/// đặc tả (khác hẳn TN 25-50, vấn đáp luôn là bộ ít câu hỏi sâu).</summary>
public sealed class GenerateOralExamSetVersionsRequestValidator : AbstractValidator<GenerateOralExamSetVersionsRequest>
{
    public GenerateOralExamSetVersionsRequestValidator()
    {
        RuleFor(x => x.Ten).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PoolOralQuestionIds).NotEmpty();
        RuleFor(x => x.TargetCount).InclusiveBetween(1, 4);
        RuleFor(x => x.VersionCount).InclusiveBetween(2, 4);
    }
}

public sealed class UpdateExamVersionLopVisibilityRequestValidator : AbstractValidator<UpdateExamVersionLopVisibilityRequest>
{
    public UpdateExamVersionLopVisibilityRequestValidator()
    {
        RuleFor(x => x.LopIds).NotNull();
    }
}

public sealed class ExportWordRequestValidator : AbstractValidator<ExportWordRequest>
{
    public ExportWordRequestValidator()
    {
        RuleFor(x => x).Must(x => (x.QuestionIds?.Count ?? 0) + (x.EssayQuestionIds?.Count ?? 0) + (x.OralQuestionIds?.Count ?? 0) > 0)
            .WithMessage("Phải chọn ít nhất 1 câu hỏi để xuất.");
    }
}

public sealed class CreateOralQuestionRequestValidator : AbstractValidator<CreateOralQuestionRequest>
{
    public CreateOralQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ExpectedAnswer).MaximumLength(4000);
        RuleFor(x => x.Difficulty).InclusiveBetween(1, 3);
    }
}

public sealed class UpdateOralQuestionRequestValidator : AbstractValidator<UpdateOralQuestionRequest>
{
    public UpdateOralQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ExpectedAnswer).MaximumLength(4000);
        RuleFor(x => x.Difficulty).InclusiveBetween(1, 3);
    }
}

public sealed class UpdateOralQuestionLopVisibilityRequestValidator : AbstractValidator<UpdateOralQuestionLopVisibilityRequest>
{
    public UpdateOralQuestionLopVisibilityRequestValidator()
    {
        RuleFor(x => x.LopIds).NotNull();
    }
}

public sealed class SubmitExamRequestValidator : AbstractValidator<SubmitExamRequest>
{
    public SubmitExamRequestValidator()
    {
        RuleFor(x => x.Answers).NotEmpty();
        RuleFor(x => x.TimeSpentSeconds).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.SelectedOption).InclusiveBetween(0, 3);
        });
    }
}

public sealed class SubmitOralRequestValidator : AbstractValidator<SubmitOralRequest>
{
    public SubmitOralRequestValidator()
    {
        RuleFor(x => x.MainAnswer).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.FollowupAnswers).Must(a => a is null || a.Count <= 10)
            .WithMessage("Tối đa 10 câu trả lời bổ sung.");
        RuleForEach(x => x.FollowupAnswers).MaximumLength(4000);
    }
}

// ===================== Việc 4.1 (2026-08-19) — Chống thoát thi thử =====================

public sealed class StartExamSessionRequestValidator : AbstractValidator<StartExamSessionRequest>
{
    public StartExamSessionRequestValidator()
    {
        RuleFor(x => x.QuestionIds).NotEmpty();
        // 1 phút .. 4 giờ — chặn giá trị vô lý (0, âm, hoặc quá lớn) mà không khoá cứng đúng 1 con
        // số, vì TN (45 phút) và VĐ (3 phút/câu, thay đổi theo số câu) có thời lượng khác nhau.
        RuleFor(x => x.ExpectedDurationSeconds).InclusiveBetween(60, 4 * 3600);
    }
}

public sealed class AutoSubmitExamRequestValidator : AbstractValidator<AutoSubmitExamRequest>
{
    public AutoSubmitExamRequestValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        // Answers CÓ THỂ rỗng — beacon lúc rời trang gửi bất kỳ câu nào đã kịp trả lời, có thể là
        // 0 câu nếu học viên thoát ngay từ đầu.
        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.SelectedOption).InclusiveBetween(0, 3);
        });
    }
}

public sealed class AbandonOralSessionRequestValidator : AbstractValidator<AbandonOralSessionRequest>
{
    public AbandonOralSessionRequestValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}
