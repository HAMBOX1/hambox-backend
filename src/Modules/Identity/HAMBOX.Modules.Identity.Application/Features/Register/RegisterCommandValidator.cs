using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Application.Referrals;

namespace HAMBOX.Modules.Identity.Application.Features.Register;

/// <summary>
/// Validator for the <see cref="RegisterCommand"/> command.
/// </summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator(IPlatformSettingsProvider platformSettings, IReferralRedemptionService referralRedemption)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters.")
            .MustAsync(async (password, cancellation) =>
            {
                var auth = await platformSettings.GetAuthenticationAsync(cancellation);
                if (password.Length < auth.MinimumPasswordLength)
                {
                    return false;
                }

                if (auth.RequireNumbers && !password.Any(char.IsDigit))
                {
                    return false;
                }

                if (auth.RequireUppercase && !password.Any(char.IsUpper))
                {
                    return false;
                }

                if (auth.RequireSymbols && !password.Any(ch => !char.IsLetterOrDigit(ch)))
                {
                    return false;
                }

                return true;
            })
            .WithMessage("Password does not meet the configured password policy.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.ReferralCode)
            .Matches(@"^HAM-[A-Z0-9]{8}$").WithMessage("The referral code format is invalid.")
            .MustAsync(async (_, cancellation) =>
            {
                var referral = await platformSettings.GetAsync<ReferralSettingsPayload>(
                    PlatformSettingsCategoryKeys.Referral, cancellation);
                return referral.Enabled;
            })
            .WithMessage("The referral program is not currently active.")
            .MustAsync(async (code, cancellation) => await referralRedemption.ReferralCodeExistsAsync(code!, cancellation))
            .WithMessage("The referral code you entered was not found.")
            .When(x => !string.IsNullOrWhiteSpace(x.ReferralCode));
    }
}
