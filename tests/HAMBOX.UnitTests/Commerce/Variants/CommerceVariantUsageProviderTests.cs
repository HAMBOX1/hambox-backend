using HAMBOX.Modules.Commerce.Application.Variants;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Variants;

/// <summary>
/// Confirms the real (not faked) Commerce-side half of variant cleanup is idempotent — required
/// for CleanupProductVariantCommandHandler's cross-module retry safety, since Catalog and Commerce
/// cleanup are two separate calls, never one distributed transaction (see
/// CleanupProductVariantCommandHandler's own doc comment).
/// </summary>
public sealed class CommerceVariantUsageProviderTests
{
    [Fact]
    public async Task RemoveCartItemsAsync_CalledTwice_SecondCallIsANoOp()
    {
        var (commerceDb, _) = CommerceTestDbContextFactory.Create();
        var variantId = Guid.NewGuid();
        var cart = ShoppingCart.CreateForUser("user-1");
        cart.AddOrUpdateItem(Guid.NewGuid(), 1, 9.99m, variantId);
        commerceDb.ShoppingCarts.Add(cart);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var provider = new CommerceVariantUsageProvider(commerceDb);

        var first = await provider.RemoveCartItemsAsync(variantId, CancellationToken.None);
        var second = await provider.RemoveCartItemsAsync(variantId, CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Empty(commerceDb.CartItems.Where(c => c.ProductVariantId == variantId));
    }
}
