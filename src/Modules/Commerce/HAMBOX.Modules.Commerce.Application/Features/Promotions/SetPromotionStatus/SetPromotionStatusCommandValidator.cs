using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.SetPromotionStatus;

public sealed class SetPromotionStatusCommandValidator : AbstractValidator<SetPromotionStatusCommand>
{
    public SetPromotionStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Action).IsInEnum();
    }
}
