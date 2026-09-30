using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.OxaPay;

public sealed class InitiateOxaPayCheckoutCommandValidator : AbstractValidator<InitiateOxaPayCheckoutCommand>
{
    public InitiateOxaPayCheckoutCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
    }
}
