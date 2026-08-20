using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

public sealed class InitiateDotCheckoutCommandValidator : AbstractValidator<InitiateDotCheckoutCommand>
{
    public InitiateDotCheckoutCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
    }
}
