using HAMBOX.Application.Security;
using Microsoft.Extensions.Options;

namespace HAMBOX.Infrastructure.Security;

/// <summary>Fail-fast validation at startup — mirrors JwtSettingsValidator/EmailSettingsValidator's pattern exactly.</summary>
internal sealed class TurnstileSettingsValidator : IValidateOptions<TurnstileSettings>
{
    public ValidateOptionsResult Validate(string? name, TurnstileSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.SiteKey))
        {
            return ValidateOptionsResult.Fail("Turnstile:SiteKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            return ValidateOptionsResult.Fail(
                "Turnstile:SecretKey is required. Set it via environment variable Turnstile__SecretKey or user secrets — never in a file tracked by source control.");
        }

        if (options.RequestTimeoutSeconds is <= 0 or > 60)
        {
            return ValidateOptionsResult.Fail("Turnstile:RequestTimeoutSeconds must be between 1 and 60.");
        }

        return ValidateOptionsResult.Success;
    }
}
