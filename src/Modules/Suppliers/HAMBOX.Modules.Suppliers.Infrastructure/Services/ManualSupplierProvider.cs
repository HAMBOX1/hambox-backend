using HAMBOX.Modules.Suppliers.Application.Abstractions;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Services;

/// <summary>
/// Default provider for suppliers with no automated integration — inventory/pricing/fulfillment for
/// these suppliers is handled entirely through the existing manual Catalog inventory workflow
/// (matches how the marketplace already operates today for every supplier). Every operation reports
/// itself as not automated rather than silently no-op-succeeding, so the admin UI can tell the
/// difference between "connected" and "nothing to connect to".
/// </summary>
internal sealed class ManualSupplierProvider : ISupplierProvider
{
    public const string Key = "Manual";

    public string ProviderType => Key;

    public Task<SupplierConnectionTestResult> TestConnectionAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierConnectionTestResult(true, "Manual supplier — no external connection required."));

    public Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierCredentialValidationResult(true, "Manual supplier — credentials are not used."));

    public Task<SupplierProductSyncResult> SyncProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierProductSyncResult(false, 0, "Manual supplier — products must be entered via Catalog inventory."));

    public Task<SupplierInventorySyncResult> SyncInventoryAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierInventorySyncResult(false, 0, "Manual supplier — inventory is managed via Catalog inventory batches."));

    public Task<SupplierPriceSyncResult> SyncPricesAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierPriceSyncResult(false, 0, "Manual supplier — prices are not synced automatically."));

    public Task<SupplierReservationResult> ReserveAsync(SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierReservationResult(false, null, "Manual supplier — reservations are not supported."));

    public Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierPurchaseResult(false, null, null, "Manual supplier — purchases are not supported."));

    public Task<SupplierCancellationResult> CancelAsync(SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierCancellationResult(false, "Manual supplier — nothing to cancel externally."));

    public Task<SupplierOrderStatusResult> GetOrderStatusAsync(string providerOrderId, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierOrderStatusResult(false, "Unknown", "Manual supplier — no external order status."));
}
