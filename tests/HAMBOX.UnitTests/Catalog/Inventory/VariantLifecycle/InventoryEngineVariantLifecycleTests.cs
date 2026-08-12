using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Covers the ProductVariant lifecycle audit's business rules directly against the real
/// <see cref="InventoryEngine"/> and <see cref="VariantUsageCalculator"/> — the actual production
/// decision logic for what's safe to remove, safe to detach, or protected history. See
/// <see cref="TestCatalogDbContext"/>'s doc comment for why this deliberately does not exercise
/// InventoryEngine's SQL Server transaction/row-lock code path.
/// </summary>
public sealed class InventoryEngineVariantLifecycleTests
{
    private static InventoryEngine CreateEngine(
        TestCatalogDbContext db, FakeCommerceVariantUsageProvider commerceUsage, string? userId = "admin-1") =>
        new(db, new FakeCurrentUserService(userId), new FakePlatformSettingsProvider(), commerceUsage);

    private static ProductVariant CreateVariant(TestCatalogDbContext db)
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}");
        db.ProductVariants.Add(variant);
        return variant;
    }

    private static InventoryBatch CreateBatch(TestCatalogDbContext db, Guid variantId)
    {
        var batch = InventoryBatch.Create(variantId, "Batch 1", null, null, "USD", 0m, null, null);
        db.InventoryBatches.Add(batch);
        return batch;
    }

    private static DigitalInventoryCode CreateCode(
        TestCatalogDbContext db, Guid variantId, Guid batchId, InventoryCodeStatus status, string code)
    {
        var digitalCode = DigitalInventoryCode.Create(variantId, batchId, code, null, null, null, null, null, "USD", null, null);
        switch (status)
        {
            case InventoryCodeStatus.Available:
                break;
            case InventoryCodeStatus.Disabled:
                digitalCode.Disable();
                break;
            case InventoryCodeStatus.Sold:
                digitalCode.Reserve();
                digitalCode.MarkSold(Guid.NewGuid(), Guid.NewGuid());
                break;
            case InventoryCodeStatus.Reserved:
                digitalCode.Reserve();
                break;
            default:
                throw new NotSupportedException($"Test helper does not build status {status} directly.");
        }

        db.DigitalInventoryCodes.Add(digitalCode);
        return digitalCode;
    }

    // ---- #1: zero references -> permanent deletion succeeds ----
    [Fact]
    public async Task DeleteVariantPermanentlyAsync_NoReferences_TombstonesVariant()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = CreateEngine(db, commerceUsage);
        await engine.DeleteVariantPermanentlyAsync(variant.Id, CancellationToken.None);

        var reloaded = await db.ProductVariants.IgnoreQueryFilters().SingleAsync(v => v.Id == variant.Id);
        Assert.True(reloaded.IsDeleted);
        Assert.Equal(ProductVariantStatus.Archived, reloaded.Status);
        Assert.False(reloaded.IsVisible);
    }

    // ---- #5: sold codes -> permanent deletion blocked ----
    [Fact]
    public async Task DeleteVariantPermanentlyAsync_SoldCode_Blocked()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        var batch = CreateBatch(db, variant.Id);
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Sold, "SOLD-CODE-1");
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = CreateEngine(db, commerceUsage);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.DeleteVariantPermanentlyAsync(variant.Id, CancellationToken.None));
        Assert.Equal("Variant has protected usage.", ex.Message);

        var reloaded = await db.ProductVariants.SingleAsync(v => v.Id == variant.Id);
        Assert.False(reloaded.IsDeleted);
    }

    // ---- #6: OrderItems -> permanent deletion blocked ----
    [Fact]
    public async Task DeleteVariantPermanentlyAsync_HasOrderItems_Blocked()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        await db.SaveChangesAsync(CancellationToken.None);
        commerceUsage.OrderItemCountByVariant[variant.Id] = 1;

        var engine = CreateEngine(db, commerceUsage);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.DeleteVariantPermanentlyAsync(variant.Id, CancellationToken.None));
        Assert.Equal("Variant has protected usage.", ex.Message);
    }

    // ---- #7: OrderLicenseKeys -> permanent deletion blocked ----
    [Fact]
    public async Task DeleteVariantPermanentlyAsync_HasOrderLicenseKeys_Blocked()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        await db.SaveChangesAsync(CancellationToken.None);
        commerceUsage.OrderLicenseKeyCountByVariant[variant.Id] = 1;

        var engine = CreateEngine(db, commerceUsage);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.DeleteVariantPermanentlyAsync(variant.Id, CancellationToken.None));
        Assert.Equal("Variant has protected usage.", ex.Message);
    }

    // ---- #2: available codes -> cleanup removes them ----
    [Fact]
    public async Task CleanupVariantAsync_AvailableAndDisabledCodes_AreRemoved()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        var batch = CreateBatch(db, variant.Id);
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Available, "AVAILABLE-1");
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Disabled, "DISABLED-1");
        var soldCode = CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Sold, "SOLD-1");
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = CreateEngine(db, commerceUsage);
        var result = await engine.CleanupVariantAsync(variant.Id, CancellationToken.None);

        Assert.Equal(2, result.CodesRemoved);
        var remaining = await db.DigitalInventoryCodes.Where(c => c.VariantId == variant.Id).ToListAsync();
        // Sold codes are fulfillment history — cleanup must never remove them.
        Assert.Equal([soldCode.Id], remaining.Select(c => c.Id));
    }

    // ---- #3: cart items -> cleanup removes them (via the Commerce-side contract) ----
    [Fact]
    public async Task CleanupVariantAsync_DoesNotTouchCommerceDirectly_CartItemCleanupIsTheProviderContract()
    {
        // CleanupVariantAsync (InventoryEngine, Catalog-only) never touches Commerce data itself —
        // that's ICommerceVariantUsageProvider.RemoveCartItemsAsync's job, called by
        // CleanupProductVariantCommandHandler (Catalog.Application) before this method. Proven at
        // that layer in CleanupProductVariantCommandHandlerTests; asserted here only that this
        // method's own DB writes are scoped to Catalog data (no CommerceVariantUsageProvider calls).
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        await db.SaveChangesAsync(CancellationToken.None);
        commerceUsage.CartItemCountByVariant[variant.Id] = 3;

        var engine = CreateEngine(db, commerceUsage);
        await engine.CleanupVariantAsync(variant.Id, CancellationToken.None);

        // Untouched by the Catalog-only engine call — still 3, proving CleanupVariantAsync doesn't
        // (and structurally can't) reach into Commerce data on its own.
        Assert.Equal(3, commerceUsage.CartItemCountByVariant[variant.Id]);
    }

    // ---- #4: active reservations -> cleanup handles them safely ----
    [Fact]
    public async Task CleanupVariantAsync_ActiveReservation_ReleasesReservationAndRemovesFreedCode()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        var batch = CreateBatch(db, variant.Id);
        var reservedCode = CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Reserved, "RESERVED-1");
        var reservation = InventoryReservation.Create(reservedCode.Id, variant.Id, "user-1", cartId: Guid.NewGuid());
        db.InventoryReservations.Add(reservation);
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = CreateEngine(db, commerceUsage);
        var result = await engine.CleanupVariantAsync(variant.Id, CancellationToken.None);

        Assert.Equal(1, result.ReservationsReleased);
        // The reservation's code flips Reserved -> Available as part of release, and — since
        // Available codes are also safe to remove — the same cleanup call removes it too.
        Assert.Equal(1, result.CodesRemoved);

        var reloadedReservation = await db.InventoryReservations.SingleAsync(r => r.Id == reservation.Id);
        Assert.False(reloadedReservation.IsActive);
        Assert.False(await db.DigitalInventoryCodes.AnyAsync(c => c.Id == reservedCode.Id));
    }

    // ---- #12: cleanup is idempotent ----
    [Fact]
    public async Task CleanupVariantAsync_CalledTwice_SecondCallFindsNothingLeft()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        var batch = CreateBatch(db, variant.Id);
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Available, "AVAILABLE-1");
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = CreateEngine(db, commerceUsage);
        var first = await engine.CleanupVariantAsync(variant.Id, CancellationToken.None);
        var second = await engine.CleanupVariantAsync(variant.Id, CancellationToken.None);

        Assert.Equal(1, first.CodesRemoved);
        Assert.Equal(0, second.ReservationsReleased);
        Assert.Equal(0, second.CodesRemoved);
    }

    // ---- #8: InventoryAuditLogs survive deletion/archive/cleanup ----
    [Fact]
    public async Task DeleteAndCleanup_NeverRemoveAuditLogRows_OnlyAppend()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        var batch = CreateBatch(db, variant.Id);
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Available, "AVAILABLE-1");
        db.InventoryAuditLogs.Add(InventoryAuditLog.Create(InventoryAuditAction.VariantCreated, variantId: variant.Id));
        await db.SaveChangesAsync(CancellationToken.None);

        var preExistingLogCount = await db.InventoryAuditLogs.CountAsync();

        var engine = CreateEngine(db, commerceUsage);
        await engine.CleanupVariantAsync(variant.Id, CancellationToken.None);
        await engine.DeleteVariantPermanentlyAsync(variant.Id, CancellationToken.None);

        var finalLogs = await db.InventoryAuditLogs.ToListAsync();
        // Only ever grows — the pre-existing row is still there, plus whatever cleanup/delete added.
        Assert.True(finalLogs.Count > preExistingLogCount);
        Assert.Contains(finalLogs, l => l.Action == InventoryAuditAction.VariantCreated);
    }

    // ---- #13 / #17: re-check trumps stale/previously-seen state ----
    [Fact]
    public async Task DeleteVariantPermanentlyAsync_UsageChangedSincePriorCheck_StillBlocked()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = CreateEngine(db, commerceUsage);

        // A caller (e.g. the frontend) checks usage and sees "clean" — nothing blocks deletion yet.
        var usageBeforeRace = await VariantUsageCalculator.ComputeAsync(db, commerceUsage, variant.Id, CancellationToken.None);
        Assert.True(usageBeforeRace.CanPermanentlyDelete);

        // Something else happens in between (a purchase completes) that the caller never re-checked.
        commerceUsage.OrderLicenseKeyCountByVariant[variant.Id] = 1;

        // The delete call itself must re-derive usage fresh, not trust the snapshot above.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.DeleteVariantPermanentlyAsync(variant.Id, CancellationToken.None));
        Assert.Equal("Variant has protected usage.", ex.Message);
    }

    // ---- #16: usage counts match actual database state ----
    [Fact]
    public async Task ComputeAsync_ReportsExactCountsPerCategory()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = CreateVariant(db);
        var batch = CreateBatch(db, variant.Id);
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Available, "A-1");
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Available, "A-2");
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Disabled, "D-1");
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Sold, "S-1");
        db.InventoryAuditLogs.Add(InventoryAuditLog.Create(InventoryAuditAction.VariantCreated, variantId: variant.Id));
        db.InventoryAuditLogs.Add(InventoryAuditLog.Create(InventoryAuditAction.BatchCreated, variantId: variant.Id));
        await db.SaveChangesAsync(CancellationToken.None);
        commerceUsage.CartItemCountByVariant[variant.Id] = 4;
        commerceUsage.OrderItemCountByVariant[variant.Id] = 2;
        commerceUsage.OrderLicenseKeyCountByVariant[variant.Id] = 1;

        var usage = await VariantUsageCalculator.ComputeAsync(db, commerceUsage, variant.Id, CancellationToken.None);

        Assert.Equal(2 + 1 + 4, usage.SafeToRemove.TotalCount); // 2 available + 1 disabled + 4 cart items
        Assert.Equal(1 + 2, usage.SafeToDetach.TotalCount); // 1 batch + 2 audit log entries
        Assert.Contains(usage.SafeToDetach.Items, i => i.Type == "InventoryBatches" && i.Count == 1);
        Assert.Contains(usage.SafeToDetach.Items, i => i.Type == "InventoryAuditLogReferences" && i.Count == 2);
        Assert.Equal(1 + 2 + 1, usage.ProtectedHistory.TotalCount); // 1 sold + 2 order items + 1 license key
        Assert.False(usage.CanPermanentlyDelete);
    }

    // ---- #18: Archive/Activate (the reversible path) never disturbs inventory state ----
    [Fact]
    public async Task Archive_ThenActivate_RestoresStatusOnly_NeverTouchesInventoryData()
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = CreateVariant(db);
        variant.Activate();
        var batch = CreateBatch(db, variant.Id);
        CreateCode(db, variant.Id, batch.Id, InventoryCodeStatus.Available, "A-1");
        await db.SaveChangesAsync(CancellationToken.None);

        variant.Archive();
        await db.SaveChangesAsync(CancellationToken.None);

        var archived = await db.ProductVariants.SingleAsync(v => v.Id == variant.Id);
        Assert.Equal(ProductVariantStatus.Archived, archived.Status);
        Assert.False(archived.IsVisible);
        // The reversible path never touches IsDeleted — this is what makes un-archiving possible at
        // all (Activate()'s lookup filters !IsDeleted).
        Assert.False(archived.IsDeleted);

        archived.Activate();
        await db.SaveChangesAsync(CancellationToken.None);

        var reactivated = await db.ProductVariants.SingleAsync(v => v.Id == variant.Id);
        Assert.Equal(ProductVariantStatus.Active, reactivated.Status);
        Assert.True(reactivated.IsVisible);
        // Nothing about the inventory was ever removed by Archive, so there is nothing to
        // "restore" — the code that was present before archiving is still there afterwards.
        Assert.Equal(1, await db.DigitalInventoryCodes.CountAsync(c => c.VariantId == variant.Id));
    }
}
