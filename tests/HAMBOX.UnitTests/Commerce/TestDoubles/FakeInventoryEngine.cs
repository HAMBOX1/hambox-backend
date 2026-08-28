using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>
/// Minimal in-memory stand-in for <see cref="IInventoryEngine"/>. <see cref="ProductHasVariantsAsync"/>
/// mirrors the real engine's predicate exactly (queries the same Catalog db context for
/// active + visible variants) since AddCartItem/Checkout's variant-required branching depends on it.
/// Stock/reservation behavior is configured per test via simple in-memory state — the real
/// InventoryEngine's locking/row-lock behavior is unchanged by H1 and out of scope here.
/// </summary>
internal sealed class FakeInventoryEngine(ICatalogDbContext catalogDb) : IInventoryEngine
{
    public Dictionary<Guid, int> AvailableStockByVariant { get; } = [];

    /// <summary>Codes available to hand out per variant, consumed in order by <see cref="ReserveCodesAsync"/>.</summary>
    public Dictionary<Guid, Queue<string>> CodesByVariant { get; } = [];

    /// <summary>Digital code text reserved per CodeId, so <see cref="CommitReservationsAsync"/> can
    /// hand back the exact code that was reserved instead of a different, made-up string.</summary>
    private readonly Dictionary<Guid, string> _reservedCodeText = [];

    /// <summary>Configurable per test; defaults to "nothing low/out of stock" for tests that only need
    /// <see cref="GetStatisticsAsync"/> to return without throwing, not to assert on stock counts.</summary>
    public InventoryStatisticsSnapshot Statistics { get; set; } = new(0, 0, 0, 0, 0, 0, 0m, 0m, 0m, 0m);

    public Task<InventoryStatisticsSnapshot> GetStatisticsAsync(
        Guid? productId = null, Guid? variantId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Statistics);

    public Task<VariantStockSnapshot> GetVariantStockAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var available = AvailableStockByVariant.GetValueOrDefault(variantId);
        return Task.FromResult(new VariantStockSnapshot(variantId, available, 0, 0, 0, 0, false, available <= 0));
    }

    public Task<IReadOnlyDictionary<Guid, VariantStockSnapshot>> GetVariantStockBulkAsync(
        IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, VariantStockSnapshot> result = variantIds.ToDictionary(
            id => id,
            id =>
            {
                var available = AvailableStockByVariant.GetValueOrDefault(id);
                return new VariantStockSnapshot(id, available, 0, 0, 0, 0, false, available <= 0);
            });
        return Task.FromResult(result);
    }

    public Task<bool> ProductHasVariantsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        catalogDb.ProductVariants.AnyAsync(
            v => v.ProductId == productId
                && !v.IsDeleted
                && v.Status == ProductVariantStatus.Active
                && v.IsVisible,
            cancellationToken);

    /// <summary>When set, <see cref="ReserveCodesAsync"/> throws this instead of its normal
    /// behavior — lets a test simulate a lower-level failure (e.g. a concurrency conflict)
    /// surfacing partway through a checkout transaction.</summary>
    public Exception? ThrowOnReserve { get; set; }

    public Task<IReadOnlyList<ReservedCodeSnapshot>> ReserveCodesAsync(
        Guid variantId, int quantity, string? userId, Guid? cartId, CancellationToken cancellationToken = default)
    {
        if (ThrowOnReserve is not null)
        {
            throw ThrowOnReserve;
        }

        var available = AvailableStockByVariant.GetValueOrDefault(variantId);
        if (available < quantity)
        {
            throw new InvalidOperationException("Insufficient inventory codes.");
        }

        return Task.FromResult(ReserveInternal(variantId, quantity));
    }

    public Task<IReadOnlyList<ReservedCodeSnapshot>> ReservePartialCodesAsync(
        Guid variantId, int maxQuantity, string? userId, Guid? cartId, CancellationToken cancellationToken = default)
    {
        if (ThrowOnReserve is not null)
        {
            throw ThrowOnReserve;
        }

        var available = AvailableStockByVariant.GetValueOrDefault(variantId);
        var toReserve = Math.Min(available, maxQuantity);
        return Task.FromResult(toReserve <= 0
            ? (IReadOnlyList<ReservedCodeSnapshot>)[]
            : ReserveInternal(variantId, toReserve));
    }

    private IReadOnlyList<ReservedCodeSnapshot> ReserveInternal(Guid variantId, int quantity)
    {
        var pool = CodesByVariant.TryGetValue(variantId, out var queue) ? queue : new Queue<string>();
        var reserved = new List<ReservedCodeSnapshot>();
        for (var i = 0; i < quantity; i++)
        {
            var code = pool.Count > 0 ? pool.Dequeue() : $"REAL-CODE-{variantId:N}-{i}";
            var codeId = Guid.NewGuid();
            _reservedCodeText[codeId] = code;
            reserved.Add(new ReservedCodeSnapshot(codeId, variantId, code, DateTimeOffset.UtcNow.AddMinutes(15)));
        }

        AvailableStockByVariant[variantId] = AvailableStockByVariant.GetValueOrDefault(variantId) - quantity;
        return reserved;
    }

    public Task ReleaseReservationsForCartAsync(Guid cartId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ReleaseReservationsAsync(IEnumerable<Guid> codeIds, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<CommittedCodeSnapshot>> CommitReservationsAsync(
        Guid orderId, IReadOnlyList<(Guid OrderItemId, Guid CodeId)> assignments, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CommittedCodeSnapshot>>(
            assignments
                .Select(a => new CommittedCodeSnapshot(a.CodeId, a.OrderItemId, _reservedCodeText[a.CodeId]))
                .ToList());

    public Task<int> ExpireStaleReservationsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<int> ReleaseSoldCodesForOrderAsync(
        Guid orderId, string? performedByUserId, CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<ImportCodesResult> ImportCodesAsync(
        Guid variantId, Guid batchId, IReadOnlyList<ImportCodeItem> codes, string? performedByUserId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by H1 tests.");

    public Task<VariantCleanupResult> CleanupVariantAsync(Guid variantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by H1 tests.");

    public Task DeleteVariantPermanentlyAsync(Guid variantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by H1 tests.");
}
