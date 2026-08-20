using HAMBOX.Modules.Commerce.Application.Options;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Validates DOT Fawry settings <em>format</em> at first access — an empty/unset section is valid
/// (DOT Fawry is an optional payment method; the API must still start without it configured), but a
/// value that IS set must be well-formed. Whether DOT Fawry is actually usable for a given checkout
/// request is a separate, per-request business check (see
/// <c>InitiateDotFawryCheckoutCommandHandler</c>), not a startup-fatal condition.
/// </summary>
internal sealed class DotFawrySettingsValidator : IValidateOptions<DotFawrySettings>
{
    public ValidateOptionsResult Validate(string? name, DotFawrySettings options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl) && !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("DotFawry:BaseUrl must be an absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(options.DirectBillingPath))
        {
            return ValidateOptionsResult.Fail("DotFawry:DirectBillingPath must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.CheckTransactionStatusPath))
        {
            return ValidateOptionsResult.Fail("DotFawry:CheckTransactionStatusPath must not be empty.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("DotFawry:RequestTimeoutSeconds must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
