using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout;

public sealed class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(50);
    }
}
