namespace HAMBOX.Modules.Suppliers.Application.Abstractions;

/// <summary>
/// Owns refreshing <c>SupplierProductAvailability</c> — the persisted cache <c>FulfillmentRouter</c>
/// reads, never queried live from the storefront/checkout path. Suppliers module owns this entirely;
/// Catalog/Commerce never call a provider or touch this table directly (see the phase's architectural
/// constraints).
/// </summary>
public interface ISupplierAvailabilityService
{
    /// <summary>
    /// Refreshes availability for one supplier's active mappings in as few provider calls as the
    /// provider allows (never one call per mapping) — a disabled supplier, one with no active mappings,
    /// or one with no provider registered is a safe no-op, not an error, so the background job's loop
    /// over every enabled supplier never needs to special-case any of those. <paramref name="triggeredByUserId"/>
    /// is <see langword="null"/> for the recurring background job and set for the admin "Sync now" action
    /// — attributed on the written <c>SupplierAuditLog</c> row, nothing else changes behavior-wise.
    /// </summary>
    Task<SupplierAvailabilitySyncResult> SyncSupplierAsync(Guid supplierId, string? triggeredByUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loops every enabled supplier with at least one active mapping, syncing each independently — one
    /// supplier's provider failure (caught inside <see cref="SyncSupplierAsync"/>) never stops or taints
    /// another supplier's sync. Called by the recurring background job.
    /// </summary>
    Task<IReadOnlyList<SupplierAvailabilitySyncResult>> SyncAllEnabledSuppliersAsync(CancellationToken cancellationToken = default);
}

/// <summary>Safe summary only — counts and timing, never provider error text beyond what's already safe on the persisted row.</summary>
public sealed record SupplierAvailabilitySyncResult(
    Guid SupplierId,
    bool IsSuccess,
    int MappingsChecked,
    int AvailableCount,
    int UnavailableCount,
    int UnknownCount,
    string? Message);
