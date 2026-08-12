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

    /// <summary>
    /// Returns every sold code tied to <paramref name="orderId"/> back to the available pool, e.g. when
    /// the order is cancelled or refunded. Codes not currently <c>Sold</c> (already released, disabled, etc.)
    /// are left untouched, so this is safe to call more than once for the same order.
    /// </summary>
    Task<int> ReleaseSoldCodesForOrderAsync(
        Guid orderId,
        string? performedByUserId,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Removes only the genuinely disposable operational data for one variant — active
    /// reservations (released through the same path as <see cref="ReleaseReservationsAsync"/>,
    /// so the paired code status flip + audit log stay correct) and Available/Disabled codes.
    /// Never touches Sold/Reserved/Returned/Expired/Lost codes, InventoryBatches, or
    /// InventoryAuditLogs. Runs as one atomic transaction; safe to call more than once (idempotent
    /// — a second call simply finds nothing left to remove).
    /// </summary>
    Task<VariantCleanupResult> CleanupVariantAsync(Guid variantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes (tombstones — see <c>ProductVariant.SoftDelete</c>) a variant. Usage is
    /// re-checked fresh inside this same call, under a row lock on the variant's inventory codes
    /// so a concurrent checkout reservation for the same variant cannot interleave with the
    /// check — never trusts counts the caller already saw. Throws <see cref="InvalidOperationException"/>
    /// with message "Variant not found." or "Variant has protected usage." for the two expected
    /// failure cases; both are caught and mapped to <c>Result</c> failures by the caller.
    /// </summary>
    Task DeleteVariantPermanentlyAsync(Guid variantId, CancellationToken cancellationToken = default);
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

/// <summary>Where a submitted code was already found — global across the whole catalog, not just the target variant/batch.</summary>
public sealed record ImportCodeDuplicate(string Code, string ProductName, string VariantSku, string? BatchName);

public sealed record ImportCodesResult(
    int TotalSubmitted,
    int Imported,
    int Duplicates,
    int Invalid,
    IReadOnlyList<ImportCodeDuplicate> DuplicateDetails);

public sealed record VariantCleanupResult(int ReservationsReleased, int CodesRemoved);

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
