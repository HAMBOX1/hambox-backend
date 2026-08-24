using FluentValidation;
using HAMBOX.Application.Security;

namespace HAMBOX.Modules.Identity.Application.Features.ResendVerification;

/// <summary>
/// Validator for the <see cref="ResendVerificationCommand"/> command.
/// </summary>
public sealed class ResendVerificationCommandValidator : AbstractValidator<ResendVerificationCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResendVerificationCommandValidator"/> class.
    /// </summary>
    public ResendVerificationCommandValidator(ITurnstileVerificationService turnstile)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.TurnstileToken)
            .MustAsync((command, token, cancellation) => turnstile.VerifyAsync(token, command.IpAddress, "resend-verification", cancellation))
            .WithMessage("Security verification failed. Please try again.");
    }
}
