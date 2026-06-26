using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.VerifyEmail;

/// <summary>
/// Validator for the <see cref="VerifyEmailCommand"/> command.
/// </summary>
public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VerifyEmailCommandValidator"/> class.
    /// </summary>
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Verification token is required.");
    }
}
