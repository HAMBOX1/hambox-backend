using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Covers #3 end to end: CleanupProductVariantCommandHandler (Catalog.Application) is the actual
/// orchestrator that calls ICommerceVariantUsageProvider.RemoveCartItemsAsync — the engine-level
/// tests only prove the Catalog side leaves Commerce data alone; this proves the handler actually
/// invokes the cross-module cleanup and reports the refreshed (zeroed) usage.
/// </summary>
public sealed class CleanupProductVariantCommandHandlerTests
{
    [Fact]
    public async Task Handle_VariantWithCartItems_RemovesThemAndReturnsRefreshedUsage()
    {
        var db = TestCatalogDbContextFactory.Create();
        var commerceUsage = new FakeCommerceVariantUsageProvider();
        var variant = ProductVariant.Create(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}");
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(CancellationToken.None);
        commerceUsage.CartItemCountByVariant[variant.Id] = 5;

        var engine = new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), commerceUsage);
        var handler = new CleanupProductVariantCommandHandler(db, engine, commerceUsage);

        var result = await handler.Handle(new CleanupProductVariantCommand(variant.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, commerceUsage.CartItemCountByVariant[variant.Id]);
        Assert.Equal(0, result.Value.SafeToRemove.TotalCount);
        Assert.True(result.Value.CanPermanentlyDelete);
    }
}
