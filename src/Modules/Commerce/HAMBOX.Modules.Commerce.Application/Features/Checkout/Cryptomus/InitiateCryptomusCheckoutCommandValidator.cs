using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Cryptomus;

public sealed class InitiateCryptomusCheckoutCommandValidator : AbstractValidator<InitiateCryptomusCheckoutCommand>
{
    public InitiateCryptomusCheckoutCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
    }
}
