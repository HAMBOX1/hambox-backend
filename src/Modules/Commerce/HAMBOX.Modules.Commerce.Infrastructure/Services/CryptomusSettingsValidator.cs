using HAMBOX.Modules.Commerce.Application.Options;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Validates Cryptomus settings <em>format</em> at first access — an empty/unset section is valid
/// (Cryptomus is an optional payment method), but a value that IS set must be well-formed. Mirrors
/// <c>DotSettingsValidator</c>.
/// </summary>
internal sealed class CryptomusSettingsValidator : IValidateOptions<CryptomusSettings>
{
    public ValidateOptionsResult Validate(string? name, CryptomusSettings options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl)
            && (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail("Cryptomus:BaseUrl must be an absolute https:// URL.");
        }

        if (!string.IsNullOrWhiteSpace(options.PublicWebhookUrl)
            && !Uri.TryCreate(options.PublicWebhookUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("Cryptomus:PublicWebhookUrl must be an absolute URL.");
        }

        if (!string.IsNullOrWhiteSpace(options.FrontendResultUrl)
            && !Uri.TryCreate(options.FrontendResultUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("Cryptomus:FrontendResultUrl must be an absolute URL.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("Cryptomus:RequestTimeoutSeconds must be greater than zero.");
        }

        if (options.InvoiceLifetimeSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("Cryptomus:InvoiceLifetimeSeconds must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
