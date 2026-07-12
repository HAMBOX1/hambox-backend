namespace HAMBOX.Modules.Catalog.Application.Abstractions;

public interface IInventoryEngine
{
    Task<VariantStockSnapshot> GetVariantStockAsync(Guid variantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, VariantStockSnapshot>> GetVariantStockBulkAsync(
        IEnumerable<Guid> variantIds,
        CancellationToken cancellationToken = default);

    Task<bool> ProductHasVariantsAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservedCodeSnapshot>> ReserveCodesAsync(
        Guid variantId,
        int quantity,
        string? userId,
        Guid? cartId,
        CancellationToken cancellationToken = default);

    Task ReleaseReservationsForCartAsync(Guid cartId, CancellationToken cancellationToken = default);

    Task ReleaseReservationsAsync(IEnumerable<Guid> codeIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommittedCodeSnapshot>> CommitReservationsAsync(
        Guid orderId,
        IReadOnlyList<(Guid OrderItemId, Guid CodeId)> assignments,
        CancellationToken cancellationToken = default);

    Task<int> ExpireStaleReservationsAsync(CancellationToken cancellationToken = default);

    Task<InventoryStatisticsSnapshot> GetStatisticsAsync(
        Guid? productId = null,
        Guid? variantId = null,
        CancellationToken cancellationToken = default);

    Task<ImportCodesResult> ImportCodesAsync(
        Guid variantId,
        Guid batchId,
        IReadOnlyList<ImportCodeItem> codes,
        string? performedByUserId,
        CancellationToken cancellationToken = default);
}

public sealed record VariantStockSnapshot(
    Guid VariantId,
    int Available,
    int Reserved,
    int Sold,
    int Expired,
    int Disabled,
    bool IsLowStock,
    bool IsOutOfStock);

public sealed record ReservedCodeSnapshot(Guid CodeId, Guid VariantId, string DigitalCode, DateTimeOffset ExpiresOnUtc);

public sealed record CommittedCodeSnapshot(Guid CodeId, Guid OrderItemId, string DigitalCode);

public sealed record ImportCodeItem(
    string DigitalCode,
    string? SerialNumber = null,
    string? Pin = null,
    decimal? PurchaseCost = null,
    DateTimeOffset? ExpirationDate = null);

public sealed record ImportCodesResult(int Imported, int Duplicates, int Invalid);

public sealed record InventoryStatisticsSnapshot(
    int Available,
    int Reserved,
    int Sold,
    int Expired,
    int LowStockVariants,
    int OutOfStockVariants,
    decimal InventoryValue,
    decimal PurchaseCost,
    decimal EstimatedRevenue,
    decimal EstimatedProfit);
