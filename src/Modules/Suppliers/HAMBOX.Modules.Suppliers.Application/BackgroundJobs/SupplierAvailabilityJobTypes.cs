namespace HAMBOX.Modules.Suppliers.Application.BackgroundJobs;

/// <summary>Mirrors <see cref="SupplierFulfillmentJobTypes"/>'s convention.</summary>
public static class SupplierAvailabilityJobTypes
{
    /// <summary>One recurring job type that syncs every enabled supplier with active mappings — not one job per supplier. See <see cref="Abstractions.ISupplierAvailabilityService.SyncAllEnabledSuppliersAsync"/>.</summary>
    public const string Sync = "SupplierAvailabilitySync";
}
