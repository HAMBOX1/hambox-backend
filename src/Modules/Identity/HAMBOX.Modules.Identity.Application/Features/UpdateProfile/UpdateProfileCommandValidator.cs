using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.UpdateProfile;

/// <summary>
/// Validator for the <see cref="UpdateProfileCommand"/> command.
/// </summary>
public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateProfileCommandValidator"/> class.
    /// </summary>
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .When(x => x.PhoneNumber is not null);

        RuleFor(x => x.PreferredLanguage)
            .Must(lang => lang is null || lang is "en" or "ar")
            .WithMessage("Preferred language must be 'en' or 'ar'.");

        RuleFor(x => x.PreferredCurrency)
            .Must(code => code is null || code is "USD" or "EUR" or "EGP" or "SAR")
            .WithMessage("Preferred currency must be USD, EUR, EGP, or SAR.");
    }
}
