using FluentValidation;
using HAMBOX.Modules.Content.Domain.Faqs;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.UpdateFaq;

public sealed class UpdateFaqCommandValidator : AbstractValidator<UpdateFaqCommand>
{
    public UpdateFaqCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.QuestionEn).NotEmpty().MaximumLength(500);
        RuleFor(x => x.QuestionAr).MaximumLength(500);
        RuleFor(x => x.AnswerEn).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();

        RuleFor(x => x.TargetId)
            .Null()
            .When(x => x.Scope == FaqScope.Global)
            .WithMessage("Global FAQs cannot have a target.");

        RuleFor(x => x.TargetId)
            .NotNull()
            .When(x => x.Scope != FaqScope.Global)
            .WithMessage("Product/Category FAQs require a target.");
    }
}
