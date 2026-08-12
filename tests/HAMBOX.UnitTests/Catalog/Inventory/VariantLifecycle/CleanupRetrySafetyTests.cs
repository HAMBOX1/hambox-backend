using HAMBOX.Application.Variants;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Documents and proves the retry-safety contract CleanupProductVariantCommandHandler's own doc
/// comment describes: Catalog cleanup and Commerce cart cleanup are two separate calls, never one
/// distributed transaction. Both halves are independently idempotent (proven separately in
/// InventoryEngineVariantLifecycleTests and CommerceVariantUsageProviderTests), so a retry after
/// either half fails is always safe regardless of which one failed first — this file proves that
/// composite claim end to end, for both failure orderings.
/// </summary>
public sealed class CleanupRetrySafetyTests
{
    /// <summary>Throws on its first call, then behaves normally — stands in for a transient failure
    /// (e.g. a dropped connection) in the Commerce-side half of cleanup.</summary>
    private sealed class ThrowOnceCommerceVariantUsageProvider : ICommerceVariantUsageProvider
    {
        private readonly FakeCommerceVariantUsageProvider _inner = new();
        private bool _hasThrown;

        public Task<CommerceVariantUsageSnapshot> GetUsageAsync(Guid variantId, CancellationToken cancellationToken = default) =>
            _inner.GetUsageAsync(variantId, cancellationToken);

        public Task<int> RemoveCartItemsAsync(Guid variantId, CancellationToken cancellationToken = default)
        {
            if (!_hasThrown)
            {
                _hasThrown = true;
                throw new InvalidOperationException("Simulated transient Commerce-side failure.");
            }

            return _inner.RemoveCartItemsAsync(variantId, cancellationToken);
        }

        public Dictionary<Guid, int> CartItemCountByVariant => _inner.CartItemCountByVariant;
    }

    /// <summary>
    /// Failure ordering 1: Commerce cleanup (the handler's first step) throws before Catalog
    /// cleanup ever runs. Retrying the whole handler must complete cleanly — nothing was partially
    /// applied on the Catalog side to conflict with.
    /// </summary>
    [Fact]
    public async Task CommerceCleanupFailsFirst_RetryCompletesCleanly()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new ThrowOnceCommerceVariantUsageProvider();
        var variant = ProductVariant.Create(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}");
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(CancellationToken.None);
        commerceUsage.CartItemCountByVariant[variant.Id] = 2;

        var engine = new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), commerceUsage);
        var handler = new CleanupProductVariantCommandHandler(db, engine, commerceUsage);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new CleanupProductVariantCommand(variant.Id), CancellationToken.None));
        // Cart items are untouched by the failed attempt — nothing was silently lost.
        Assert.Equal(2, commerceUsage.CartItemCountByVariant[variant.Id]);

        var retryResult = await handler.Handle(new CleanupProductVariantCommand(variant.Id), CancellationToken.None);

        Assert.True(retryResult.IsSuccess);
        Assert.Equal(0, commerceUsage.CartItemCountByVariant[variant.Id]);
        Assert.Equal(0, retryResult.Value.SafeToRemove.TotalCount);
    }

    /// <summary>
    /// Failure ordering 2: an earlier attempt already completed the Catalog-side cleanup (codes
    /// removed, reservations released) but never reached/completed the Commerce side. Running the
    /// handler again — which always does Commerce first, then Catalog — must not error or double
    /// count just because the Catalog side has nothing left to do this time.
    /// </summary>
    [Fact]
    public async Task CatalogCleanupAlreadyCompletedByEarlierAttempt_RetryStillFinishesCommerceSideSafely()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = ProductVariant.Create(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}");
        var batch = InventoryBatch.Create(variant.Id, "Batch 1", null, null, "USD", 0m, null, null);
        db.ProductVariants.Add(variant);
        db.InventoryBatches.Add(batch);
        var code = DigitalInventoryCode.Create(variant.Id, batch.Id, "AVAILABLE-1");
        db.DigitalInventoryCodes.Add(code);
        await db.SaveChangesAsync(CancellationToken.None);
        commerceUsage.CartItemCountByVariant[variant.Id] = 3;

        var engine = new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), commerceUsage);

        // Simulates an earlier attempt that got as far as the Catalog side (e.g. the process was
        // killed right after this call, before the handler's Commerce step ever ran).
        var earlierAttemptResult = await engine.CleanupVariantAsync(variant.Id, CancellationToken.None);
        Assert.Equal(1, earlierAttemptResult.CodesRemoved);

        var handler = new CleanupProductVariantCommandHandler(db, engine, commerceUsage);
        var retryResult = await handler.Handle(new CleanupProductVariantCommand(variant.Id), CancellationToken.None);

        Assert.True(retryResult.IsSuccess);
        // Commerce side actually got cleaned this time.
        Assert.Equal(0, commerceUsage.CartItemCountByVariant[variant.Id]);
        // Catalog side found nothing left — no error, no double-removal attempt.
        Assert.Equal(0, retryResult.Value.SafeToRemove.TotalCount);
    }
}
