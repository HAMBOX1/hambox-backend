using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Features.Checkout;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// M3 fix: a legitimate <see cref="DbUpdateConcurrencyException"/> raised partway through the
/// checkout transaction (e.g. the legacy Product.RowVersion optimistic-concurrency race) must be
/// mapped to the existing friendly Products.ConcurrencyConflict error, not surfaced as a raw 500.
///
/// Also covers Sprint 4's Job → Worker order-execution change: checkout must never call the
/// automated-supplier step inline, and Order.Complete() must never fire before every required unit
/// actually has a license key.
/// </summary>
public sealed class CheckoutCommandHandlerTests
{
    private sealed class FailingPaymentProvider : IPaymentProvider
    {
        public string ProviderName => "development";
        public bool CanHandle(string paymentMethod) => true;
        public Task<PaymentProviderResult> ProcessAsync(PaymentProviderRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentProviderResult(false, "Failed", null, ProviderName, "Card declined."));
    }

    private static async Task<(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext CommerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext CatalogDb,
        FakeCurrentUserService CurrentUser,
        FakeInventoryEngine InventoryEngine,
        Product Product,
        HAMBOX.Modules.Catalog.Domain.Inventory.ProductVariant Variant)>
        SeedManualOnlyProductWithStockAsync(int stock)
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
        await catalogDb.SaveChangesAsync();

        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var cart = ShoppingCart.CreateForUser(currentUser.UserId!);
        cart.AddOrUpdateItem(product.Id, 1, product.Price, variant.Id);
        commerceDb.ShoppingCarts.Add(cart);
        await commerceDb.SaveChangesAsync();

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = stock;

        return (commerceDb, catalogDb, currentUser, inventoryEngine, product, variant);
    }

    private static CheckoutCommandHandler BuildHandler(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext commerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb,
        FakeCurrentUserService currentUser,
        FakeInventoryEngine inventoryEngine,
        IPaymentProvider paymentProvider,
        FakeOperationalJobQueue jobQueue,
        HAMBOX.Application.Fulfillment.IFulfillmentRouter? fulfillmentRouter = null)
    {
        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);
        var referralRewardService = new ReferralRewardService(commerceDb, new FakeMembershipEngine());
        var settingsProvider = new FakeCommerceSettingsProvider();
        var referralLifecycle = new ReferralLifecycleService(
            commerceDb,
            settingsProvider,
            referralRewardService,
            new FakeCommunicationService(),
            NullLogger<ReferralLifecycleService>.Instance);

        return new CheckoutCommandHandler(
            commerceDb,
            catalogDb,
            LegalTestDbContextFactory.Create(),
            new FakeCommerceTransactionService(),
            currentUser,
            inventoryEngine,
            cartResponseBuilder,
            new CartLineValidator(inventoryEngine, fulfillmentRouter ?? new FakeFulfillmentRouter(), new NullSupplierPricingEngine()),
            new PromotionRedemptionService(commerceDb),
            [paymentProvider],
            new FakeCommunicationService(),
            new FakeMembershipAccessProvider(),
            referralLifecycle,
            jobQueue,
            settingsProvider,
            NullLogger<CheckoutCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_PaymentFails_NoOrderCreated_NoFulfillmentJobEnqueued()
    {
        var (commerceDb, catalogDb, currentUser, inventoryEngine, _, _) = await SeedManualOnlyProductWithStockAsync(stock: 5);
        var jobQueue = new FakeOperationalJobQueue();
        var handler = BuildHandler(commerceDb, catalogDb, currentUser, inventoryEngine, new FailingPaymentProvider(), jobQueue);

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development", "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(commerceDb.Orders);
        Assert.Empty(jobQueue.EnqueuedJobTypes);
    }

    [Fact]
    public async Task Handle_ManualStockFullyCoversOrder_CompletesInline_NoFulfillmentJobEnqueued()
    {
        var (commerceDb, catalogDb, currentUser, inventoryEngine, _, _) = await SeedManualOnlyProductWithStockAsync(stock: 5);
        var jobQueue = new FakeOperationalJobQueue();
        var handler = BuildHandler(commerceDb, catalogDb, currentUser, inventoryEngine, new FakePaymentProvider(), jobQueue);

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development", "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persistedOrder = commerceDb.Orders.Single();
        Assert.Equal(OrderStatus.Completed, persistedOrder.Status);
        Assert.Empty(jobQueue.EnqueuedJobTypes);
    }

    [Fact]
    public async Task Handle_NoManualStock_SupplierOnlyVariant_OrderStaysProcessing_FulfillmentJobEnqueued_NotExecutedInline()
    {
        var (commerceDb, catalogDb, currentUser, inventoryEngine, _, variant) = await SeedManualOnlyProductWithStockAsync(stock: 0);
        var fulfillmentRouter = new FakeFulfillmentRouter();
        fulfillmentRouter.SetReadiness(
            variant.Id,
            new HAMBOX.Application.Fulfillment.FulfillmentReadiness(
                HAMBOX.Modules.Catalog.Domain.Enums.FulfillmentMode.SupplierOnly,
                ManualAllowed: false,
                new HAMBOX.Application.Fulfillment.FulfillmentSupplierCandidate(Guid.NewGuid(), Guid.NewGuid())));
        var jobQueue = new FakeOperationalJobQueue();
        var handler = BuildHandler(
            commerceDb, catalogDb, currentUser, inventoryEngine, new FakePaymentProvider(), jobQueue, fulfillmentRouter);

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development", "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persistedOrder = commerceDb.Orders.Single();

        // The core Sprint 4 fix: never Completed before the automated-supplier step has even run.
        Assert.Equal(OrderStatus.Processing, persistedOrder.Status);
        Assert.Empty(commerceDb.OrderLicenseKeys);

        // Job → Worker, not inline: checkout enqueued the job and returned — it never called the
        // supplier step itself (there is no supplier service in this handler's dependency graph at all
        // any more; if checkout tried to call one inline, this test simply couldn't compile it in).
        var enqueuedJobType = Assert.Single(jobQueue.EnqueuedJobTypes);
        Assert.Equal(OperationalJobTypes.ExecuteOrderFulfillment, enqueuedJobType);
    }

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
        var settingsProvider = new FakeCommerceSettingsProvider();
        var referralLifecycle = new ReferralLifecycleService(
            commerceDb,
            settingsProvider,
            referralRewardService,
            new FakeCommunicationService(),
            NullLogger<ReferralLifecycleService>.Instance);

        var handler = new CheckoutCommandHandler(
            commerceDb,
            catalogDb,
            LegalTestDbContextFactory.Create(),
            new FakeCommerceTransactionService(),
            currentUser,
            inventoryEngine,
            cartResponseBuilder,
            new CartLineValidator(inventoryEngine, new FakeFulfillmentRouter(), new NullSupplierPricingEngine()),
            new PromotionRedemptionService(commerceDb),
            [new FakePaymentProvider()],
            new FakeCommunicationService(),
            new FakeMembershipAccessProvider(),
            referralLifecycle,
            new FakeOperationalJobQueue(),
            settingsProvider,
            NullLogger<CheckoutCommandHandler>.Instance);

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development", "127.0.0.1", "test-agent", "en"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.ProductConcurrencyConflict.Code, result.Error.Code);
        Assert.Empty(commerceDb.Orders);
    }
}
