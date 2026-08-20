using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;

public sealed class InitiateDotFawryCheckoutCommandValidator : AbstractValidator<InitiateDotFawryCheckoutCommand>
{
    public InitiateDotFawryCheckoutCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);

        // DOT's Direct Billing API documents "msisdn" as a mandatory field with no format spec
        // beyond "the mobile number of the customer" — digits only (with an optional leading +) is
        // the least-assumption validation until DOT confirms an expected format (country-code
        // prefix, leading zero handling, etc.) for the Fawry service specifically.
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\+?[0-9]{7,15}$")
            .WithMessage("Enter a valid mobile number.");

        RuleFor(x => x.CustomerName).MaximumLength(256);
    }
}
