using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.GoogleLogin;

/// <summary>
/// Validator for the <see cref="GoogleLoginCommand"/> command.
/// </summary>
public sealed class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleLoginCommandValidator"/> class.
    /// </summary>
    public GoogleLoginCommandValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("Google ID token is required.");
    }
}
