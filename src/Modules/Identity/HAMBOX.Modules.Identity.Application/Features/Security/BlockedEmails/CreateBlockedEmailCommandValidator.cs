using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedEmails;

internal sealed class CreateBlockedEmailCommandValidator : AbstractValidator<CreateBlockedEmailCommand>
{
    public CreateBlockedEmailCommandValidator()
    {
        RuleFor(x => x.Pattern)
            .NotEmpty()
            .MaximumLength(320)
            .Must(p => p.Contains('@'))
            .WithMessage("Pattern must be an email address or a '*@domain.com' wildcard.");

        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.ExpiresOnUtc)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(x => x.ExpiresOnUtc.HasValue)
            .WithMessage("Expiration must be in the future.");
    }
}
