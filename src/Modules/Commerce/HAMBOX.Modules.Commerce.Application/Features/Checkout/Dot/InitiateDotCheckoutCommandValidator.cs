using FluentValidation;
using HAMBOX.Modules.Commerce.Application.Options;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

public sealed class InitiateDotCheckoutCommandValidator : AbstractValidator<InitiateDotCheckoutCommand>
{
    private static readonly string[] ValidWallets = Enum.GetNames<DotWalletOperator>();

    public InitiateDotCheckoutCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Wallet)
            .NotEmpty()
            .Must(wallet => ValidWallets.Contains(wallet, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Select a valid payment wallet.");
    }
}
