using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.DuplicatePromotion;

public sealed class DuplicatePromotionCommandValidator : AbstractValidator<DuplicatePromotionCommand>
{
    public DuplicatePromotionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.NewName).MaximumLength(200);

        RuleFor(x => x.NewName)
            .NotEmpty()
            .WithMessage("New name cannot be empty or whitespace.")
            .When(x => x.NewName is not null);
    }
}
