using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Suppliers.Domain.Suppliers;

/// <summary>Mirrors <see cref="Application.Abstractions.SupplierAvailabilityState"/> (BuildingBlocks-free — the
/// Domain layer cannot reference Suppliers.Application) — kept in sync by hand, exactly like the rest
/// of this module's provider-agnostic vocabulary.</summary>
public enum SupplierAvailabilityState
{
    Unknown = 0,
    Available = 1,
    Unavailable = 2,
}

/// <summary>
/// The last known answer from a supplier's own availability signal for one <see cref="SupplierProductMapping"/> —
/// a persisted cache, refreshed by a background sync, never queried live from the storefront/checkout
/// path (see <c>ISupplierAvailabilityService</c>). One row per mapping: even when two mappings point at
/// the same <see cref="ExternalProductId"/> (a variant-specific and a product-wide mapping to the same
/// supplier product), each gets its own row, since <c>FulfillmentRouter</c> resolves readiness per
/// mapping, not per external id.
/// </summary>
/// <remarks>
/// Starts <see cref="SupplierAvailabilityState.Unknown"/> and stays that way until the first successful
/// sync — a brand-new mapping must never appear purchasable before HAMBOX has actually asked the
/// supplier (see the phase's "Initial / Unknown State" requirement). <see cref="LastCheckedAtUtc"/> is
/// null exactly when <see cref="AvailabilityState"/> is still the initial Unknown with no sync attempt
/// yet; a failed sync attempt still updates <see cref="LastErrorMessage"/> without moving the state away
/// from whatever it already was (see <c>SupplierAvailabilityService</c>'s update logic) — a transient
/// provider failure must not erase the last known-good answer.
/// </remarks>
public sealed class SupplierProductAvailability : AggregateRoot, IAuditable
{
    private SupplierProductAvailability()
    {
    }

    private SupplierProductAvailability(
        Guid id, Guid supplierId, Guid supplierProductMappingId, string externalProductId)
        : base(id)
    {
        SupplierId = supplierId;
        SupplierProductMappingId = supplierProductMappingId;
        ExternalProductId = externalProductId;
        AvailabilityState = SupplierAvailabilityState.Unknown;
    }

    public Guid SupplierId { get; private set; }
    public Guid SupplierProductMappingId { get; private set; }
    public string ExternalProductId { get; private set; } = string.Empty;
    public SupplierAvailabilityState AvailabilityState { get; private set; }
    public int? AvailableQuantity { get; private set; }
    public DateTimeOffset? LastCheckedAtUtc { get; private set; }

    /// <summary>Safe, provider-supplied text only (mirrors <c>SupplierFulfillment.FailureDetail</c>'s
    /// discipline) — never a raw exception, never anything that could carry a credential or secret.</summary>
    public string? LastErrorMessage { get; private set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static SupplierProductAvailability CreateUnknown(Guid supplierId, Guid supplierProductMappingId, string externalProductId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProductId);
        return new SupplierProductAvailability(Guid.NewGuid(), supplierId, supplierProductMappingId, externalProductId.Trim());
    }

    /// <summary>A successful sync result — always moves <see cref="AvailabilityState"/> and clears any prior error, since this is a definite, fresh answer.</summary>
    public void RecordChecked(SupplierAvailabilityState state, int? availableQuantity, DateTimeOffset checkedAtUtc, string externalProductId)
    {
        AvailabilityState = state;
        AvailableQuantity = availableQuantity;
        LastCheckedAtUtc = checkedAtUtc;
        LastErrorMessage = null;
        // The mapping's own ExternalProductId may have been edited by an admin since this row was
        // created — keep this row's copy in sync so the (SupplierId, ExternalProductId) lookup index
        // used by the sync service's per-supplier update pass stays correct.
        ExternalProductId = externalProductId;
    }

    /// <summary>
    /// A failed sync attempt (provider unreachable, credentials broken, etc.) — deliberately does NOT
    /// touch <see cref="AvailabilityState"/>/<see cref="AvailableQuantity"/>/<see cref="LastCheckedAtUtc"/>.
    /// A transient failure must never erase the last known-good state; staleness (via
    /// <see cref="LastCheckedAtUtc"/> aging past the configured threshold) is what eventually makes
    /// <c>FulfillmentRouter</c> stop trusting it, not this call overwriting it with a guess.
    /// </summary>
    public void RecordSyncFailed(string? safeErrorMessage) => LastErrorMessage = safeErrorMessage;
}
