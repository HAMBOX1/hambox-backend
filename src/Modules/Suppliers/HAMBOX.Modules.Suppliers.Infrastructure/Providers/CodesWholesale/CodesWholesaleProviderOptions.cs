using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.CodesWholesale;

/// <summary>
/// Non-secret CodesWholesale HTTP client tuning, bound from configuration section
/// <c>"CodesWholesale"</c>. Contains nothing sensitive — Client ID/Client Secret come from the encrypted
/// <c>Supplier.ApiKey</c>/<c>Supplier.ApiSecret</c> fields, never from here — mirrors
/// <c>BambooProviderOptions</c>/<c>EnebaProviderOptions</c>'s identical shape.
/// </summary>
public sealed class CodesWholesaleProviderOptions
{
    public const string SectionName = "CodesWholesale";

    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>Hard cap on response body size, defends against a misbehaving/compromised endpoint streaming an unbounded body.</summary>
    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Bounds how far back <c>GetOrderStatusAsync</c>'s order-history reconciliation fallback searches
    /// when no <c>orderId</c> was ever captured for an ambiguous purchase (see that method's remarks) —
    /// never unbounded, since <c>/v2/orders</c>'s history filter is a plain date range with no
    /// clientOrderId search parameter of its own.
    /// </summary>
    public int ReconciliationLookbackDays { get; set; } = 7;
}

/// <summary>Fail-fast validation at startup — mirrors BambooProviderOptionsValidator/EnebaProviderOptionsValidator's identical pattern.</summary>
internal sealed class CodesWholesaleProviderOptionsValidator : IValidateOptions<CodesWholesaleProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, CodesWholesaleProviderOptions options)
    {
        if (options.RequestTimeoutSeconds is <= 0 or > 120)
        {
            return ValidateOptionsResult.Fail("CodesWholesale:RequestTimeoutSeconds must be between 1 and 120.");
        }

        if (options.MaxResponseBytes is <= 0 or > 16 * 1024 * 1024)
        {
            return ValidateOptionsResult.Fail("CodesWholesale:MaxResponseBytes must be between 1 and 16,777,216 (16 MB).");
        }

        if (options.ReconciliationLookbackDays is <= 0 or > 60)
        {
            return ValidateOptionsResult.Fail("CodesWholesale:ReconciliationLookbackDays must be between 1 and 60.");
        }

        return ValidateOptionsResult.Success;
    }
}
