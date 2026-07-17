using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Security.CountryRestrictions;

internal sealed class SetCountryRestrictionCommandValidator : AbstractValidator<SetCountryRestrictionCommand>
{
    public SetCountryRestrictionCommandValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.ExpiresOnUtc)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(x => x.ExpiresOnUtc.HasValue)
            .WithMessage("Expiration must be in the future.");
    }
}
