using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Covers #15: deleting an option group can cascade-remove every variant that uses one of its
/// options (a real FK — ProductVariantOption). That cascade must go through the exact same usage
/// gate as a direct variant delete, and must be all-or-nothing: if even one affected variant has
/// protected history, nothing is mutated — not the variant, not the option group.
/// </summary>
public sealed class DeleteProductOptionGroupCommandHandlerTests
{
    private static (ProductOptionGroup Group, ProductOption Option) CreatePlatformGroup(Guid productId)
    {
        var group = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var option = group.AddOption("steam", "Steam", 0);
        return (group, option);
    }

    private static DeleteProductOptionGroupCommandHandler CreateHandler(
        TestCatalogDbContext db, InventoryEngine engine, FakeCommerceVariantUsageProvider commerceUsage)
    {
        var cleanupSender = new DispatchingFakeSender<CleanupProductVariantCommand, Result<VariantUsageDto>>(
            new CleanupProductVariantCommandHandler(db, engine, commerceUsage));
        return new DeleteProductOptionGroupCommandHandler(db, engine, commerceUsage, cleanupSender);
    }

    [Fact]
    public async Task Handle_WithoutForce_AnyAffectedVariant_FailsFast_NothingMutated()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var productId = Guid.NewGuid();
        var (group, option) = CreatePlatformGroup(productId);
        db.ProductOptionGroups.Add(group);

        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        variant.SetOptions([option.Id]);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), commerceUsage);
        var handler = CreateHandler(db, engine, commerceUsage);

        var result = await handler.Handle(new DeleteProductOptionGroupCommand(group.Id, Force: false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.OptionGroupInUse.Code, result.Error.Code);
        Assert.True(await db.ProductOptionGroups.AnyAsync(g => g.Id == group.Id));
        Assert.False((await db.ProductVariants.SingleAsync(v => v.Id == variant.Id)).IsDeleted);
    }

    [Fact]
    public async Task Handle_ForceWithProtectedVariant_BlocksEntireOperation_NothingMutated()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var productId = Guid.NewGuid();
        var (group, option) = CreatePlatformGroup(productId);
        db.ProductOptionGroups.Add(group);

        var safeVariant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        safeVariant.SetOptions([option.Id]);
        var protectedVariant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        protectedVariant.SetOptions([option.Id]);
        db.ProductVariants.AddRange(safeVariant, protectedVariant);

        var batch = InventoryBatch.Create(protectedVariant.Id, "Batch 1", null, null, "USD", 0m, null, null);
        db.InventoryBatches.Add(batch);
        var soldCode = DigitalInventoryCode.Create(protectedVariant.Id, batch.Id, "SOLD-CODE-1");
        soldCode.Reserve();
        soldCode.MarkSold(Guid.NewGuid(), Guid.NewGuid());
        db.DigitalInventoryCodes.Add(soldCode);
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), commerceUsage);
        var handler = CreateHandler(db, engine, commerceUsage);

        var result = await handler.Handle(new DeleteProductOptionGroupCommand(group.Id, Force: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.VariantHasProtectedUsage.Code, result.Error.Code);
        // All-or-nothing: the group survives, and — crucially — the SAFE variant is also untouched,
        // even though it alone would have been deletable.
        Assert.True(await db.ProductOptionGroups.AnyAsync(g => g.Id == group.Id));
        Assert.False((await db.ProductVariants.SingleAsync(v => v.Id == safeVariant.Id)).IsDeleted);
        Assert.False((await db.ProductVariants.SingleAsync(v => v.Id == protectedVariant.Id)).IsDeleted);
    }

    [Fact]
    public async Task Handle_ForceWithOnlySafeVariants_CascadeDeletesVariantsAndRemovesGroup()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var productId = Guid.NewGuid();
        var (group, option) = CreatePlatformGroup(productId);
        db.ProductOptionGroups.Add(group);

        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        variant.SetOptions([option.Id]);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), commerceUsage);
        var handler = CreateHandler(db, engine, commerceUsage);

        var result = await handler.Handle(new DeleteProductOptionGroupCommand(group.Id, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(await db.ProductOptionGroups.AnyAsync(g => g.Id == group.Id));
        var reloadedVariant = await db.ProductVariants.IgnoreQueryFilters().SingleAsync(v => v.Id == variant.Id);
        Assert.True(reloadedVariant.IsDeleted);
    }

    [Fact]
    public async Task Handle_ForceWithRemovableButNotProtectedData_AutoCleansUpThenDeletes()
    {
        // Reproduces the reported case: a variant still has one unsold, never-reserved "Available"
        // inventory code — removable, not protected — which used to block a forced group delete
        // outright and force a separate manual "Clean Up" step first. Force should now clean it up
        // and proceed, exactly like the manual Clean Up action followed by Delete Permanently would.
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var productId = Guid.NewGuid();
        var (group, option) = CreatePlatformGroup(productId);
        db.ProductOptionGroups.Add(group);

        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        variant.SetOptions([option.Id]);
        db.ProductVariants.Add(variant);

        var batch = InventoryBatch.Create(variant.Id, "Batch 1", null, null, "USD", 0m, null, null);
        db.InventoryBatches.Add(batch);
        var availableCode = DigitalInventoryCode.Create(variant.Id, batch.Id, "AVAILABLE-CODE-1");
        db.DigitalInventoryCodes.Add(availableCode);
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), commerceUsage);
        var handler = CreateHandler(db, engine, commerceUsage);

        var result = await handler.Handle(new DeleteProductOptionGroupCommand(group.Id, Force: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(await db.ProductOptionGroups.AnyAsync(g => g.Id == group.Id));
        var reloadedVariant = await db.ProductVariants.IgnoreQueryFilters().SingleAsync(v => v.Id == variant.Id);
        Assert.True(reloadedVariant.IsDeleted);
        Assert.False(await db.DigitalInventoryCodes.AnyAsync(c => c.Id == availableCode.Id));
    }
}
