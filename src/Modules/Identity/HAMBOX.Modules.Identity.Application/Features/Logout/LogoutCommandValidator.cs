using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Logout;

/// <summary>
/// Validator for the <see cref="LogoutCommand"/> command.
/// </summary>
public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutCommandValidator"/> class.
    /// </summary>
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
