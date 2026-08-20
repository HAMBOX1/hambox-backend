using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Application.Options;

/// <summary>
/// Bound from configuration section <c>"SupplierAvailability"</c> — plain <c>IConfiguration</c>, not
/// Platform Settings, matching every other recurring-job interval in this codebase (all registered as
/// constants at their <c>Program.cs</c> call site; none go through the Platform Settings JSON-blob
/// mechanism). Lives in Application (not Infrastructure, where <see cref="Providers.Bamboo.BambooProviderOptions"/>
/// lives) because <c>Commerce.Application</c>'s <c>FulfillmentRouter</c> needs <see cref="StaleAfterMinutes"/>
/// too, and Commerce.Application cannot reference Suppliers.Infrastructure.
/// </summary>
public sealed class SupplierAvailabilityOptions
{
    public const string SectionName = "SupplierAvailability";

    /// <summary>How often the background sync job refreshes every enabled supplier's availability.</summary>
    public int SyncIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// A stored <c>Available</c> answer older than this is treated as untrustworthy by <c>FulfillmentRouter</c>
    /// — never blindly trusted as still-current. See the phase's stale-data policy: fresh+Available is
    /// eligible, everything else (stale, Unavailable, Unknown) is not.
    /// </summary>
    public int StaleAfterMinutes { get; set; } = 10;
}

/// <summary>
/// Fail-fast validation at startup — mirrors Bamboo's own <c>BambooProviderOptionsValidator</c> pattern.
/// Public (unlike that one) because it's registered from Suppliers.Infrastructure, a different assembly
/// than this Application-layer options type.
/// </summary>
public sealed class SupplierAvailabilityOptionsValidator : IValidateOptions<SupplierAvailabilityOptions>
{
    public ValidateOptionsResult Validate(string? name, SupplierAvailabilityOptions options)
    {
        if (options.SyncIntervalMinutes is <= 0 or > 1440)
        {
            return ValidateOptionsResult.Fail("SupplierAvailability:SyncIntervalMinutes must be between 1 and 1440.");
        }

        if (options.StaleAfterMinutes is <= 0 or > 1440)
        {
            return ValidateOptionsResult.Fail("SupplierAvailability:StaleAfterMinutes must be between 1 and 1440.");
        }

        return ValidateOptionsResult.Success;
    }
}
