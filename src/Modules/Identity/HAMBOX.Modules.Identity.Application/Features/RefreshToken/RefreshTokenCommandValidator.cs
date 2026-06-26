using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.RefreshToken;

/// <summary>
/// Validator for the <see cref="RefreshTokenCommand"/> command.
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTokenCommandValidator"/> class.
    /// </summary>
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
