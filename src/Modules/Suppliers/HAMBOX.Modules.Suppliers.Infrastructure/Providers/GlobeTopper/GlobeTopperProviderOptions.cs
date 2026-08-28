using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.GlobeTopper;

/// <summary>
/// Non-secret GlobeTopper HTTP client tuning, bound from configuration section <c>"GlobeTopper"</c>.
/// Contains nothing sensitive — credentials come from the encrypted <c>Supplier</c> entity, never from
/// here — mirrors <c>BambooProviderOptions</c>/<c>VisoriaProviderOptions</c> exactly for
/// <see cref="RequestTimeoutSeconds"/>/<see cref="MaxResponseBytes"/>.
/// </summary>
/// <remarks>
/// <see cref="PurchaserEmail"/>/<see cref="PurchaserFirstName"/>/<see cref="PurchaserLastName"/> exist
/// because GlobeTopper's documented Purchase endpoint (<c>POST /transaction/do-by-product/{{id}}/{{amount}}</c>)
/// requires <c>email</c>/<c>first_name</c>/<c>last_name</c> as required form fields — a real customer
/// identity concept the generic <see cref="Application.Abstractions.SupplierPurchaseRequest"/> has no
/// field for and the automated-supplier checkout path does not collect anywhere today (the same kind of
/// genuine capability gap as Visoria's <c>recharge_data</c>). Rather than plumbing real customer PII
/// through the shared, provider-agnostic purchase contract for one provider, these are a configured
/// placeholder identity. GlobeTopper's documentation does not state whether this address is used only as
/// an internal record or also triggers a direct customer-facing notification email from GlobeTopper's own
/// systems (bypassing HAMBOX's own delivery pipeline) — <b>this must be confirmed with GlobeTopper support
/// before enabling this integration for real, production purchases</b>. Defaults deliberately point at a
/// non-existent placeholder domain so an unconfigured deployment fails obviously (a bounced/undeliverable
/// address) rather than silently emailing a real address nobody intended.
/// </remarks>
public sealed class GlobeTopperProviderOptions
{
    public const string SectionName = "GlobeTopper";

    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>Hard cap on response body size read from GlobeTopper, defends against a misbehaving/compromised endpoint streaming an unbounded body.</summary>
    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    public string PurchaserEmail { get; set; } = "orders@hambox.invalid";

    public string PurchaserFirstName { get; set; } = "HAMBOX";

    public string PurchaserLastName { get; set; } = "Fulfillment";
}

/// <summary>Fail-fast validation at startup — mirrors BambooProviderOptionsValidator/VisoriaProviderOptionsValidator's identical pattern.</summary>
internal sealed class GlobeTopperProviderOptionsValidator : IValidateOptions<GlobeTopperProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, GlobeTopperProviderOptions options)
    {
        if (options.RequestTimeoutSeconds is <= 0 or > 120)
        {
            return ValidateOptionsResult.Fail("GlobeTopper:RequestTimeoutSeconds must be between 1 and 120.");
        }

        if (options.MaxResponseBytes is <= 0 or > 16 * 1024 * 1024)
        {
            return ValidateOptionsResult.Fail("GlobeTopper:MaxResponseBytes must be between 1 and 16,777,216 (16 MB).");
        }

        if (string.IsNullOrWhiteSpace(options.PurchaserEmail) || !options.PurchaserEmail.Contains('@'))
        {
            return ValidateOptionsResult.Fail("GlobeTopper:PurchaserEmail must be a non-empty, plausible email address.");
        }

        if (string.IsNullOrWhiteSpace(options.PurchaserFirstName) || string.IsNullOrWhiteSpace(options.PurchaserLastName))
        {
            return ValidateOptionsResult.Fail("GlobeTopper:PurchaserFirstName and PurchaserLastName must not be empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
