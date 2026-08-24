using FluentValidation;
using HAMBOX.Application.Security;

namespace HAMBOX.Modules.Identity.Application.Features.ForgotPassword;

/// <summary>
/// Validator for the <see cref="ForgotPasswordCommand"/> command.
/// </summary>
public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ForgotPasswordCommandValidator"/> class.
    /// </summary>
    public ForgotPasswordCommandValidator(ITurnstileVerificationService turnstile)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.TurnstileToken)
            .MustAsync((command, token, cancellation) => turnstile.VerifyAsync(token, command.IpAddress, "forgot-password", cancellation))
            .WithMessage("Security verification failed. Please try again.");
    }
}
