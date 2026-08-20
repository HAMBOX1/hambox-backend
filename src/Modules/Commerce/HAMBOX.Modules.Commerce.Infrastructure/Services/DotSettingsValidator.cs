using HAMBOX.Modules.Commerce.Application.Options;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Validates DOT settings <em>format</em> at first access — an empty/unset section is valid (DOT
/// is an optional payment method; the API must still start without it configured), but a value
/// that IS set must be well-formed. Whether DOT is actually usable for a given checkout request is
/// a separate, per-request business check (see <c>InitiateDotCheckoutCommandHandler</c>), not a
/// startup-fatal condition — unlike <c>JwtSettings</c>/<c>EmailSettings</c>, nothing else in the
/// app depends on DOT being configured.
/// </summary>
internal sealed class DotSettingsValidator : IValidateOptions<DotSettings>
{
    public ValidateOptionsResult Validate(string? name, DotSettings options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl)
            && (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail("Dot:BaseUrl must be an absolute https:// URL.");
        }

        // PublicRedirectUrl/FrontendResultUrl allow http:// too — local dev points these at
        // localhost (DOT can't reach a real callback there anyway; the full redirect round-trip is
        // only testable via a tunnel or in a deployed environment), matching how EmailSettings.
        // ApplicationBaseUrl is treated. Production deployment is still expected to use https.
        if (!string.IsNullOrWhiteSpace(options.PublicRedirectUrl)
            && !Uri.TryCreate(options.PublicRedirectUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("Dot:PublicRedirectUrl must be an absolute URL.");
        }

        if (!string.IsNullOrWhiteSpace(options.FrontendResultUrl)
            && !Uri.TryCreate(options.FrontendResultUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("Dot:FrontendResultUrl must be an absolute URL.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("Dot:RequestTimeoutSeconds must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
