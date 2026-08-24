using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Features.Checkout;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
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
            new CartLineValidator(inventoryEngine, new FakeFulfillmentRouter()),
            new PromotionRedemptionService(commerceDb),
            [new FakePaymentProvider()],
            new FakeCommunicationService(),
            new FakeMembershipAccessProvider(),
            referralLifecycle,
            new OrderFulfillmentService(
                commerceDb, inventoryEngine, new NullSupplierFulfillmentService(), new FakeFulfillmentRouter(), NullLogger<OrderFulfillmentService>.Instance),
            NullLogger<CheckoutCommandHandler>.Instance);

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.ProductConcurrencyConflict.Code, result.Error.Code);
        Assert.Empty(commerceDb.Orders);
    }

    /// <summary>
    /// Bug #2 fix: payment success alone must never mean the order is Completed. For
    /// SupplierFirst/SupplierOnly lines, checkout deliberately reserves no manual inventory (see
    /// CheckoutCommandHandler's reservation loop), so no <see cref="OrderLicenseKey"/> exists for
    /// them yet at the point payment succeeds — the order must stay Processing until fulfillment
    /// (automated supplier, or its manual fallback) actually delivers the required keys, using the
    /// same required-vs-delivered criterion <see cref="OrderFulfillmentService.FulfillMissingAsync"/>
    /// and <see cref="CommerceOrderLicenseKeyDeliverySink"/> already use.
    /// </summary>
    [Fact]
    public async Task Handle_SupplierBackedProduct_NoSupplierAvailable_PaidButNotCompleted()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, variant) = await CreateVariantAsync(catalogDb, FulfillmentMode.SupplierOnly);

        var router = new FakeFulfillmentRouter();
        router.SetReadiness(
            variant.Id,
            new FulfillmentReadiness(FulfillmentMode.SupplierOnly, false, new FulfillmentSupplierCandidate(Guid.NewGuid(), Guid.NewGuid())));

        // Mirrors a real async supplier (Bamboo): the purchase call only ever confirms acceptance —
        // it never delivers codes directly (see ISupplierFulfillmentDeliverySink's remarks) — so
        // immediately after checkout there is genuinely nothing delivered yet.
        var supplierService = new FakePendingSupplierFulfillmentService();
        var (handler, _) = await BuildHandlerAsync(commerceDb, catalogDb, product, variant, router, supplierService);

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(commerceDb.Orders);
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.NotEqual(OrderStatus.Completed, order.Status);
        Assert.Empty(commerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id));
    }

    /// <summary>
    /// Bug #2 fix, requirement 6: an automated-supplier attempt that fails (the equivalent of Bamboo
    /// rejecting the request) must never be mistaken for success — the order stays Processing
    /// (recoverable by the existing retry job / admin retry, unchanged by this fix), it is not left
    /// falsely Completed, and checkout itself does not fail even though the supplier call failed.
    /// </summary>
    [Fact]
    public async Task Handle_SupplierBackedProduct_SupplierRequestFails_PaidButNotCompleted()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, variant) = await CreateVariantAsync(catalogDb, FulfillmentMode.SupplierOnly);

        var router = new FakeFulfillmentRouter();
        router.SetReadiness(
            variant.Id,
            new FulfillmentReadiness(FulfillmentMode.SupplierOnly, false, new FulfillmentSupplierCandidate(Guid.NewGuid(), Guid.NewGuid())));

        var supplierService = new NullSupplierFulfillmentService();
        var (handler, _) = await BuildHandlerAsync(commerceDb, catalogDb, product, variant, router, supplierService);

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(commerceDb.Orders);
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Equal(1, supplierService.RequestFulfillmentCallCount);
    }

    /// <summary>
    /// Bug #2 fix: once an automated supplier actually delivers the required code(s) — via the
    /// unmodified <see cref="CommerceOrderLicenseKeyDeliverySink"/>, the real production code path a
    /// successful Bamboo delivery notification runs through — the order must flip to Completed.
    /// </summary>
    [Fact]
    public async Task Handle_SupplierBackedProduct_SupplierDelivers_OrderCompletes()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, variant) = await CreateVariantAsync(catalogDb, FulfillmentMode.SupplierOnly);

        var supplierId = Guid.NewGuid();
        var mappingId = Guid.NewGuid();
        var router = new FakeFulfillmentRouter();
        router.SetReadiness(
            variant.Id,
            new FulfillmentReadiness(FulfillmentMode.SupplierOnly, false, new FulfillmentSupplierCandidate(supplierId, mappingId)));

        var (handler, inventoryEngine) = await BuildHandlerAsync(
            commerceDb, catalogDb, product, variant, router, new NullSupplierFulfillmentService());

        var checkoutResult = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development"),
            CancellationToken.None);

        Assert.True(checkoutResult.IsSuccess);
        var order = Assert.Single(commerceDb.Orders);
        Assert.Equal(OrderStatus.Processing, order.Status);

        var orderItem = Assert.Single(order.Items);

        var sink = new CommerceOrderLicenseKeyDeliverySink(
            commerceDb, catalogDb, inventoryEngine, new FakeCommerceTransactionService(), NullLogger<CommerceOrderLicenseKeyDeliverySink>.Instance);

        await sink.OnDeliveredAsync(
            order.Id, orderItem.Id, supplierId, mappingId, isScopeExhausted: true, deliveredCodes: ["SUPPLIER-CODE-0001"], CancellationToken.None);

        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    /// <summary>Bug #2 fix, requirement 3: once every required line item's license keys exist —
    /// spread across two separate manually-fulfilled lines — the order completes.</summary>
    [Fact]
    public async Task Handle_MultipleManualLines_AllFullyStocked_OrderCompletes()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (productA, variantA) = await CreateVariantAsync(catalogDb, FulfillmentMode.ManualOnly);
        var (productB, variantB) = await CreateVariantAsync(catalogDb, FulfillmentMode.ManualOnly);

        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var cart = ShoppingCart.CreateForUser(currentUser.UserId!);
        cart.AddOrUpdateItem(productA.Id, 1, productA.Price, variantA.Id);
        cart.AddOrUpdateItem(productB.Id, 1, productB.Price, variantB.Id);
        commerceDb.ShoppingCarts.Add(cart);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variantA.Id] = 1;
        inventoryEngine.AvailableStockByVariant[variantB.Id] = 1;

        var handler = BuildHandler(commerceDb, catalogDb, currentUser, inventoryEngine, new FakeFulfillmentRouter(), new NullSupplierFulfillmentService());

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(commerceDb.Orders);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(2, commerceDb.OrderLicenseKeys.Count(k => k.OrderId == order.Id));
    }

    /// <summary>Bug #2 fix, requirement 4: one line fully manually stocked, the other
    /// supplier-backed with nothing delivered yet — partial fulfillment must never complete the
    /// order.</summary>
    [Fact]
    public async Task Handle_MixedManualAndSupplierLines_PartialFulfillment_PaidButNotCompleted()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (manualProduct, manualVariant) = await CreateVariantAsync(catalogDb, FulfillmentMode.ManualOnly);
        var (supplierProduct, supplierVariant) = await CreateVariantAsync(catalogDb, FulfillmentMode.SupplierOnly);

        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var cart = ShoppingCart.CreateForUser(currentUser.UserId!);
        cart.AddOrUpdateItem(manualProduct.Id, 1, manualProduct.Price, manualVariant.Id);
        cart.AddOrUpdateItem(supplierProduct.Id, 1, supplierProduct.Price, supplierVariant.Id);
        commerceDb.ShoppingCarts.Add(cart);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[manualVariant.Id] = 1;

        var router = new FakeFulfillmentRouter();
        router.SetReadiness(
            supplierVariant.Id,
            new FulfillmentReadiness(FulfillmentMode.SupplierOnly, false, new FulfillmentSupplierCandidate(Guid.NewGuid(), Guid.NewGuid())));

        var handler = BuildHandler(commerceDb, catalogDb, currentUser, inventoryEngine, router, new NullSupplierFulfillmentService());

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(commerceDb.Orders);
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Single(commerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id));
    }

    /// <summary>Bug #2 fix, requirement 5: a plain manually-fulfilled product must keep completing
    /// immediately at checkout, exactly as before this fix.</summary>
    [Fact]
    public async Task Handle_ManualOnlyProduct_FullyStocked_OrderCompletesImmediately()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, variant) = await CreateVariantAsync(catalogDb, FulfillmentMode.ManualOnly);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = 1;

        var (handler, _) = await BuildHandlerAsync(
            commerceDb, catalogDb, product, variant, new FakeFulfillmentRouter(), new NullSupplierFulfillmentService(), inventoryEngine);

        var result = await handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(commerceDb.Orders);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Single(commerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id));
    }

    private static async Task<(Product Product, ProductVariant Variant)> CreateVariantAsync(
        HAMBOX.Modules.Catalog.Application.Abstractions.ICatalogDbContext catalogDb, FulfillmentMode mode)
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 19.99m, category.Id);
        product.Activate();
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        variant.SetFulfillmentMode(mode);

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        return (product, variant);
    }

    private static async Task<(CheckoutCommandHandler Handler, FakeInventoryEngine InventoryEngine)> BuildHandlerAsync(
        HAMBOX.Modules.Commerce.Application.Abstractions.ICommerceDbContext commerceDb,
        HAMBOX.Modules.Catalog.Application.Abstractions.ICatalogDbContext catalogDb,
        Product product,
        ProductVariant variant,
        FakeFulfillmentRouter router,
        ISupplierFulfillmentService supplierService,
        FakeInventoryEngine? existingInventoryEngine = null)
    {
        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var cart = ShoppingCart.CreateForUser(currentUser.UserId!);
        cart.AddOrUpdateItem(product.Id, 1, product.Price, variant.Id);
        commerceDb.ShoppingCarts.Add(cart);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = existingInventoryEngine ?? new FakeInventoryEngine(catalogDb);
        var handler = BuildHandler(commerceDb, catalogDb, currentUser, inventoryEngine, router, supplierService);

        return (handler, inventoryEngine);
    }

    private static CheckoutCommandHandler BuildHandler(
        HAMBOX.Modules.Commerce.Application.Abstractions.ICommerceDbContext commerceDb,
        HAMBOX.Modules.Catalog.Application.Abstractions.ICatalogDbContext catalogDb,
        FakeCurrentUserService currentUser,
        FakeInventoryEngine inventoryEngine,
        FakeFulfillmentRouter router,
        ISupplierFulfillmentService supplierService)
    {
        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);
        var referralRewardService = new ReferralRewardService(commerceDb, new FakeMembershipEngine());
        var referralLifecycle = new ReferralLifecycleService(
            commerceDb,
            new FakeDotPlatformSettingsProvider(),
            referralRewardService,
            new FakeCommunicationService(),
            NullLogger<ReferralLifecycleService>.Instance);

        return new CheckoutCommandHandler(
            commerceDb,
            catalogDb,
            new FakeCommerceTransactionService(),
            currentUser,
            inventoryEngine,
            cartResponseBuilder,
            new CartLineValidator(inventoryEngine, router),
            new PromotionRedemptionService(commerceDb),
            [new FakePaymentProvider()],
            new FakeCommunicationService(),
            new FakeMembershipAccessProvider(),
            referralLifecycle,
            new OrderFulfillmentService(
                commerceDb, inventoryEngine, supplierService, router, NullLogger<OrderFulfillmentService>.Instance),
            NullLogger<CheckoutCommandHandler>.Instance);
    }

    /// <summary>Mirrors an async automated supplier (Bamboo): every request is accepted (Submitted)
    /// but nothing is ever delivered synchronously — real code always learns about delivered codes
    /// later, via <see cref="HAMBOX.Modules.Suppliers.Application.Abstractions.ISupplierFulfillmentDeliverySink"/>,
    /// never as a direct return value from this fake.</summary>
    private sealed class FakePendingSupplierFulfillmentService : ISupplierFulfillmentService
    {
        public Task<Result<SupplierFulfillmentOutcome>> RequestFulfillmentAsync(
            SupplierFulfillmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new SupplierFulfillmentOutcome(
                Guid.NewGuid(), Guid.NewGuid(), SupplierFulfillmentStatus.Submitted,
                request.RequestedQuantity, 0, request.RequestedQuantity, null, null)));

        public Task<Result<SupplierFulfillmentOutcome>> ProcessAsync(Guid fulfillmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new SupplierFulfillmentOutcome(
                fulfillmentId, Guid.NewGuid(), SupplierFulfillmentStatus.Submitted, 1, 0, 1, null, null)));

        public Task<Result<SupplierFulfillmentOutcome>> ReconcileAsync(Guid fulfillmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not needed by this test.");

        public Task<SupplierFulfillmentSweepResult> ProcessDueFulfillmentsAsync(int batchSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not needed by this test.");

        public Task<bool> IsScopeExhaustedAsync(
            Guid orderId, Guid orderItemId, Guid supplierId, Guid supplierProductMappingId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
