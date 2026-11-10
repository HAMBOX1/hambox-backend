using FluentValidation;
using HAMBOX.Application.Abstractions;

namespace HAMBOX.Modules.Identity.Application.Features.ResetPassword;

/// <summary>
/// Validator for the <see cref="ResetPasswordCommand"/> command.
/// </summary>
public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResetPasswordCommandValidator"/> class.
    /// </summary>
    public ResetPasswordCommandValidator(IPlatformSettingsProvider platformSettings)
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MaximumLength(128).WithMessage("New password must not exceed 128 characters.")
            .MeetsConfiguredPasswordPolicy(platformSettings);
    }
}
