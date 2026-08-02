using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.DeletePromotion;

public sealed class DeletePromotionCommandValidator : AbstractValidator<DeletePromotionCommand>
{
    public DeletePromotionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
