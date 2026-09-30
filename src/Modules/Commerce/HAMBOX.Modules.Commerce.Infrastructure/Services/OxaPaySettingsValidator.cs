using HAMBOX.Modules.Commerce.Application.Options;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Validates OxaPay settings <em>format</em> at first access — an empty/unset section is valid
/// (OxaPay is an optional payment method), but a value that IS set must be well-formed. Mirrors
/// <c>CryptomusSettingsValidator</c>.
/// </summary>
internal sealed class OxaPaySettingsValidator : IValidateOptions<OxaPaySettings>
{
    public ValidateOptionsResult Validate(string? name, OxaPaySettings options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl)
            && (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail("OxaPay:BaseUrl must be an absolute https:// URL.");
        }

        if (!string.IsNullOrWhiteSpace(options.PublicWebhookUrl)
            && !Uri.TryCreate(options.PublicWebhookUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("OxaPay:PublicWebhookUrl must be an absolute URL.");
        }

        if (!string.IsNullOrWhiteSpace(options.FrontendResultUrl)
            && !Uri.TryCreate(options.FrontendResultUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("OxaPay:FrontendResultUrl must be an absolute URL.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("OxaPay:RequestTimeoutSeconds must be greater than zero.");
        }

        if (options.InvoiceLifetimeMinutes is <= 0 or > 2880)
        {
            return ValidateOptionsResult.Fail("OxaPay:InvoiceLifetimeMinutes must be between 1 and 2880.");
        }

        return ValidateOptionsResult.Success;
    }
}
