using FluentValidation;
using HAMBOX.Application.Abstractions;

namespace HAMBOX.Modules.Identity.Application.Features.ChangePassword;

/// <summary>
/// Validator for the <see cref="ChangePasswordCommand"/> command.
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordCommandValidator"/> class.
    /// </summary>
    public ChangePasswordCommandValidator(IPlatformSettingsProvider platformSettings)
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MaximumLength(128).WithMessage("New password must not exceed 128 characters.")
            .MeetsConfiguredPasswordPolicy(platformSettings)
            .Must((command, newPassword) => newPassword != command.CurrentPassword)
            .WithMessage("The new password must be different from the current password.");
    }
}
