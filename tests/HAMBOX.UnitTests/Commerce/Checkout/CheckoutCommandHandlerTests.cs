using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Features.Checkout;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// M3 fix: a legitimate <see cref="DbUpdateConcurrencyException"/> raised partway through the
/// checkout transaction (e.g. the legacy Product.RowVersion optimistic-concurrency race) must be
/// mapped to the existing friendly Products.ConcurrencyConflict error, not surfaced as a raw 500.
/// </summary>
public sealed class CheckoutCommandHandlerTests
{
    [Fact]
    public async Task Handle_ConcurrencyConflictDuringInventoryReservation_ReturnsFriendlyError()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();

        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 19.99m, category.Id);
        product.Activate();
        var variant = HAMBOX.Modules.Catalog.Domain.Inventory.ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var cart = ShoppingCart.CreateForUser(currentUser.UserId!);
        cart.AddOrUpdateItem(product.Id, 1, product.Price, variant.Id);
        commerceDb.ShoppingCarts.Add(cart);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = 5;
        // Simulates a lost optimistic-concurrency race surfacing during the reservation step —
        // the exact spot the pre-fix code would have let bubble up as an unhandled 500.
        inventoryEngine.ThrowOnReserve = new DbUpdateConcurrencyException("Simulated concurrency conflict.");

        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);
        var referralRewardService = new ReferralRewardService(commerceDb, new FakeMembershipEngine());
        var referralLifecycle = new ReferralLifecycleService(
            commerceDb,
            new FakePlatformSettingsProvider(),
            referralRewardService,
            new FakeCommunicationService(),
            NullLogger<ReferralLifecycleService>.Instance);

        var handler = new CheckoutCommandHandler(
            commerceDb,
            catalogDb,
            new FakeCommerceTransactionService(),
            currentUser,
            inventoryEngine,
            cartResponseBuilder,
            [new FakePaymentProvider()],
            new FakeCommunicationService(),
            new FakeMembershipAccessProvider(),
            referralLifecycle,
            NullLogger<CheckoutCommandHandler>.Instance);

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.ProductConcurrencyConflict.Code, result.Error.Code);
        Assert.Empty(commerceDb.Orders);
    }
}
