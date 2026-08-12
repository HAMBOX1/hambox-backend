using FluentValidation;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaqCategory;

public sealed class CreateFaqCategoryCommandValidator : AbstractValidator<CreateFaqCategoryCommand>
{
    public CreateFaqCategoryCommandValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameAr).MaximumLength(100);
    }
}
