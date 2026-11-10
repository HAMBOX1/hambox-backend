using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;

namespace HAMBOX.Modules.Identity.Application.Features;

/// <summary>
/// The configured password policy (Settings > Authentication) as one reusable rule, so registration,
/// change-password and reset-password all enforce the same thing instead of each hard-coding its own minimum.
/// </summary>
public static class PasswordPolicyRules
{
    public static IRuleBuilderOptions<T, string> MeetsConfiguredPasswordPolicy<T>(
        this IRuleBuilder<T, string> rule,
        IPlatformSettingsProvider platformSettings) =>
        rule.MustAsync(async (password, cancellation) =>
            {
                // A missing password ("{}" binds to null) must fail validation, not throw.
                if (string.IsNullOrEmpty(password))
                {
                    return false;
                }

                var auth = await platformSettings.GetAuthenticationAsync(cancellation);
                return password.Length >= auth.MinimumPasswordLength
                    && (!auth.RequireNumbers || password.Any(char.IsDigit))
                    && (!auth.RequireUppercase || password.Any(char.IsUpper))
                    && (!auth.RequireSymbols || password.Any(ch => !char.IsLetterOrDigit(ch)));
            })
            .WithMessage("Password does not meet the configured password policy.");
}

/// <summary>Input rules for free-text profile fields, shared by registration and profile update.</summary>
public static class ProfileInputRules
{
    /// <summary>
    /// Names are shown in admin screens and e-mails; markup characters have no place in a person's name,
    /// so they are refused rather than stored and escaped everywhere they are rendered.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> WithoutMarkup<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => value is null || value.IndexOfAny(['<', '>']) < 0)
            .WithMessage("{PropertyName} must not contain '<' or '>'.");

    /// <summary>Digits with an optional leading '+', spaces, dashes and brackets — no letters.</summary>
    public static IRuleBuilderOptions<T, string?> PhoneFormat<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(@"^\+?[0-9 ()\-]{6,20}$")
            .WithMessage("Phone number is not valid.");
}
