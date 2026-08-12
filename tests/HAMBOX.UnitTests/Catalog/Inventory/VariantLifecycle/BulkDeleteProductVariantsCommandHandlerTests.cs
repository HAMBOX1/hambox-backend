using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Covers #14: bulk deletion must apply the exact same per-item safety rules as a single delete —
/// never "ignore errors and delete everything". Each id is attempted independently through the
/// real single-delete handler; some may legitimately succeed while others are blocked.
/// </summary>
public sealed class BulkDeleteProductVariantsCommandHandlerTests
{
    [Fact]
    public async Task Handle_MixOfSafeAndProtectedVariants_DeletesOnlyTheSafeOne()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var productId = Guid.NewGuid();

        var safeVariant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        var protectedVariant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        db.ProductVariants.AddRange(safeVariant, protectedVariant);

        var batch = InventoryBatch.Create(protectedVariant.Id, "Batch 1", null, null, "USD", 0m, null, null);
        db.InventoryBatches.Add(batch);
        var soldCode = DigitalInventoryCode.Create(protectedVariant.Id, batch.Id, "SOLD-CODE-1");
        soldCode.Reserve();
        soldCode.MarkSold(Guid.NewGuid(), Guid.NewGuid());
        db.DigitalInventoryCodes.Add(soldCode);
        await db.SaveChangesAsync(CancellationToken.None);

        var engine = new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), commerceUsage);
        var deleteHandler = new DeleteProductVariantCommandHandler(engine);
        var sender = new DispatchingFakeSender<DeleteProductVariantCommand, Result>(deleteHandler);
        var bulkHandler = new BulkDeleteProductVariantsCommandHandler(sender);

        var result = await bulkHandler.Handle(
            new BulkDeleteProductVariantsCommand(productId, [safeVariant.Id, protectedVariant.Id]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.SuccessCount);
        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Contains(protectedVariant.Id.ToString(), result.Value.Errors[0]);
        Assert.Equal([protectedVariant.Id], result.Value.BlockedVariantIds);

        var reloadedSafe = await db.ProductVariants.IgnoreQueryFilters().SingleAsync(v => v.Id == safeVariant.Id);
        var reloadedProtected = await db.ProductVariants.SingleAsync(v => v.Id == protectedVariant.Id);
        Assert.True(reloadedSafe.IsDeleted);
        // Never force-deleted despite being part of the same bulk request.
        Assert.False(reloadedProtected.IsDeleted);
    }
}
