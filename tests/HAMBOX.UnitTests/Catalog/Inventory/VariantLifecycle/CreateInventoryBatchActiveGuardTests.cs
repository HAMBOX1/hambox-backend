using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Covers #11: an archived (or otherwise non-Active) variant must never receive new inventory —
/// the batch/code-creation guard added alongside the lifecycle rework.
/// </summary>
public sealed class CreateInventoryBatchActiveGuardTests
{
    [Fact]
    public async Task Handle_ArchivedVariant_RejectsNewBatch()
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = ProductVariant.Create(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        variant.Archive();
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateInventoryBatchCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var result = await handler.Handle(
            new CreateInventoryBatchCommand(variant.Id, "Batch 1", null, null, "USD", 0m, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.VariantNotActive.Code, result.Error.Code);
        Assert.False(await db.InventoryBatches.AnyAsync(b => b.VariantId == variant.Id));
    }

    [Fact]
    public async Task Handle_ActiveVariant_AllowsNewBatch()
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = ProductVariant.Create(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateInventoryBatchCommandHandler(db, new FakeCurrentUserService("admin-1"));
        var result = await handler.Handle(
            new CreateInventoryBatchCommand(variant.Id, "Batch 1", null, null, "USD", 0m, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
