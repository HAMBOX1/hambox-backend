using HAMBOX.Infrastructure.Currency;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Features.Checkout;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Options;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using DomainAvailabilityState = HAMBOX.Modules.Suppliers.Domain.Suppliers.SupplierAvailabilityState;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// End-to-end checkout coverage for the supplier-cost-derived pricing feature: the cheapest eligible
/// supplier's cost+margin determines the storefront price, checkout recalculates that price
/// server-side (never trusting a stale/tampered cart row), and the winning supplier/cost/margin is
/// snapshotted onto the created <c>OrderItem</c> immutably.
/// </summary>
public sealed class SupplierPricedCheckoutTests
{
    private sealed record Harness(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext CommerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext CatalogDb,
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext SuppliersDb,
        CheckoutCommandHandler Handler,
        FakeCurrentUserService CurrentUser,
        HAMBOX.UnitTests.Commerce.Dot.TestDoubles.FakeOperationalJobQueue JobQueue);

    private static async Task<(Harness Harness, Product Product, HAMBOX.Modules.Catalog.Domain.Inventory.ProductVariant Variant)>
        CreateHarnessWithSupplierFirstVariantAsync(decimal defaultMarginPercent = 20m)
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();

        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 999m, category.Id); // deliberately far from any supplier price, to prove it's never used
        product.Activate();
        var variant = HAMBOX.Modules.Catalog.Domain.Inventory.ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        variant.SetFulfillmentMode(FulfillmentMode.SupplierFirst);

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var cart = ShoppingCart.CreateForUser(currentUser.UserId!);
        // Deliberately seeded with a WRONG (too low) price, simulating a stale cart row or a
        // client-tampered value — checkout must recompute and never trust this.
        cart.AddOrUpdateItem(product.Id, 1, unitPrice: 0.01m, variant.Id);
        commerceDb.ShoppingCarts.Add(cart);
        await commerceDb.SaveChangesAsync();

        var providerA = new FakeSupplierProvider("Bamboo");
        var providerB = new FakeSupplierProvider("Visoria");
        var registry = new SupplierProviderRegistry([providerA, providerB]);

        var supplierA = Supplier.Create("Bamboo", $"SUP-{Guid.NewGuid():N}", "Bamboo", SupplierAuthenticationType.None, null, priority: 0);
        var supplierB = Supplier.Create("Visoria", $"SUP-{Guid.NewGuid():N}", "Visoria", SupplierAuthenticationType.None, null, priority: 0);
        suppliersDb.Suppliers.AddRange(supplierA, supplierB);

        // The user's own worked example: A=$7.45/20%=$8.94, B=$6.90/20%=$8.28 -> B must win.
        var mappingA = SupplierProductMapping.Create(supplierA.Id, product.Id, "EXT-A", null, null, 7.45m, "USD", 0, variant.Id);
        var mappingB = SupplierProductMapping.Create(supplierB.Id, product.Id, "EXT-B", null, null, 6.90m, "USD", 0, variant.Id);
        suppliersDb.SupplierProductMappings.AddRange(mappingA, mappingB);
        await suppliersDb.SaveChangesAsync();

        foreach (var (supplierId, mapping, externalId) in new[] { (supplierA.Id, mappingA, "EXT-A"), (supplierB.Id, mappingB, "EXT-B") })
        {
            var availability = SupplierProductAvailability.CreateUnknown(supplierId, mapping.Id, externalId);
            availability.RecordChecked(DomainAvailabilityState.Available, null, DateTimeOffset.UtcNow, externalId);
            suppliersDb.SupplierProductAvailabilities.Add(availability);
        }
        await suppliersDb.SaveChangesAsync();

        var exchangeRateService = new CurrencyExchangeRateService(
            new FakeCurrencyExchangeRateProvider(),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CurrencySettings()),
            TimeProvider.System,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        var routingEngine = new SupplierRoutingEngine(suppliersDb, registry, exchangeRateService, Options.Create(new SupplierAvailabilityOptions()));
        var settingsProvider = new FakeCommerceSettingsProvider
        {
            Commerce = new(0m, false, 15, 24, 14, "INV-", DefaultSupplierMarginPercent: defaultMarginPercent),
        };
        var pricingEngine = new SupplierPricingEngine(routingEngine, settingsProvider);

        var fulfillmentRouter = new FakeFulfillmentRouter();
        fulfillmentRouter.SetReadiness(
            variant.Id,
            new HAMBOX.Application.Fulfillment.FulfillmentReadiness(
                FulfillmentMode.SupplierFirst, ManualAllowed: false,
                new HAMBOX.Application.Fulfillment.FulfillmentSupplierCandidate(supplierB.Id, mappingB.Id)));

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var cartLineValidator = new CartLineValidator(inventoryEngine, fulfillmentRouter, pricingEngine);
        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);
        var referralRewardService = new ReferralRewardService(commerceDb, new FakeMembershipEngine());
        var referralLifecycle = new ReferralLifecycleService(
            commerceDb, settingsProvider, referralRewardService, new FakeCommunicationService(),
            NullLogger<ReferralLifecycleService>.Instance);
        var jobQueue = new FakeOperationalJobQueue();
        var handler = new CheckoutCommandHandler(
            commerceDb, catalogDb, LegalTestDbContextFactory.Create(), new FakeCommerceTransactionService(), currentUser, inventoryEngine,
            cartResponseBuilder, cartLineValidator, new PromotionRedemptionService(commerceDb),
            [new FakePaymentProvider()], new FakeCommunicationService(), new FakeMembershipAccessProvider(),
            referralLifecycle, jobQueue, settingsProvider, NullLogger<CheckoutCommandHandler>.Instance);

        return (new Harness(commerceDb, catalogDb, suppliersDb, handler, currentUser, jobQueue), product, variant);
    }

    // 1 & 4 & 8: cheapest supplier (by selling price) determines the storefront/checkout price; the
    // more expensive supplier is never selected; checkout price matches what the pricing engine computes.
    [Fact]
    public async Task Checkout_TwoSuppliersDifferentCost_CheapestSellingPriceWinsAndIsCharged()
    {
        var (harness, _, _) = await CreateHarnessWithSupplierFirstVariantAsync();

        var result = await harness.Handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var orderItem = Assert.Single(result.Value.Items);
        Assert.Equal(8.28m, orderItem.UnitPrice);
    }

    // 7: the frontend-submitted (stale/tampered) cart price can never override the backend-calculated
    // price — the seeded cart row carried 0.01, the created order must reflect the real $8.28.
    [Fact]
    public async Task Checkout_StaleOrTamperedCartPrice_IsIgnored_ServerRecalculatesAuthoritatively()
    {
        var (harness, _, _) = await CreateHarnessWithSupplierFirstVariantAsync();

        var result = await harness.Handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var orderItem = Assert.Single(result.Value.Items);
        Assert.NotEqual(0.01m, orderItem.UnitPrice);
        Assert.Equal(8.28m, orderItem.UnitPrice);
    }

    // 11: supplier acquisition cost is never exposed on the customer-facing OrderDto.
    [Fact]
    public async Task Checkout_OrderDto_NeverExposesSupplierAcquisitionCost()
    {
        var (harness, _, _) = await CreateHarnessWithSupplierFirstVariantAsync();

        var result = await harness.Handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // OrderDto/OrderItemDto (Contracts) intentionally have no BuyingPrice/SupplierId/margin fields —
        // this compiles at all only because those fields don't exist on the DTO type; if someone adds
        // one, update this test to assert it's never populated for a customer-context caller instead.
        var dtoType = result.Value.Items.Single().GetType();
        Assert.DoesNotContain(dtoType.GetProperties(), p => p.Name.Contains("BuyingPrice", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dtoType.GetProperties(), p => p.Name.Contains("SupplierId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dtoType.GetProperties(), p => p.Name.Contains("Margin", StringComparison.OrdinalIgnoreCase));
    }

    // 9 & 10: what's persisted on OrderItem is a frozen snapshot — proven directly against the domain
    // entity (not the customer DTO, which deliberately excludes these fields per the test above).
    [Fact]
    public async Task Checkout_OrderItem_SnapshotsSelectedSupplierCostAndMargin()
    {
        var (harness, _, variant) = await CreateHarnessWithSupplierFirstVariantAsync();

        var result = await harness.Handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persistedOrder = harness.CommerceDb.Orders.Single();
        var persistedItem = persistedOrder.Items.Single(i => i.ProductVariantId == variant.Id);

        Assert.NotNull(persistedItem.SelectedSupplierId);
        Assert.NotNull(persistedItem.SelectedSupplierProductMappingId);
        Assert.Equal(6.90m, persistedItem.SupplierBuyingPriceAtOrderTime);
        Assert.Equal(20m, persistedItem.MarginPercentAppliedAtOrderTime);
        Assert.Equal(8.28m, persistedItem.UnitPrice);

        // A later change to the winning supplier's mapping (cost or margin) must never retroactively
        // alter this already-created order — the snapshot columns are copied once, never re-read.
        var mapping = harness.SuppliersDb.SupplierProductMappings.Single(m => m.SupplierId == persistedItem.SelectedSupplierId);
        mapping.Update("EXT-B", null, null, buyingPrice: 1.00m, currency: "USD", priority: 0, status: SupplierMappingStatus.Active);
        await harness.SuppliersDb.SaveChangesAsync();

        var reloadedItem = harness.CommerceDb.Orders.Single().Items.Single(i => i.ProductVariantId == variant.Id);
        Assert.Equal(8.28m, reloadedItem.UnitPrice);
        Assert.Equal(6.90m, reloadedItem.SupplierBuyingPriceAtOrderTime);
    }

    // Sprint 4: SupplierFirst has no manual stock reserved inline (see the reservation loop's own
    // skip-for-Supplier*-modes comment), so this order must never complete synchronously at checkout —
    // it must stay Processing until the automated-supplier job (enqueued here, executed by the worker)
    // actually delivers the code. Checkout must return successfully regardless; the customer isn't
    // blocked on the external supplier call.
    [Fact]
    public async Task Checkout_SupplierFirstVariant_OrderStaysProcessing_AndEnqueuesTheFulfillmentJob_NeverCallsSupplierInline()
    {
        var (harness, _, _) = await CreateHarnessWithSupplierFirstVariantAsync();

        var result = await harness.Handler.Handle(
            new CheckoutCommand("buyer@example.com", "US", "development", "127.0.0.1", "test-agent", "en"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persistedOrder = harness.CommerceDb.Orders.Single();
        Assert.Equal(OrderStatus.Processing, persistedOrder.Status);

        var enqueuedJobType = Assert.Single(harness.JobQueue.EnqueuedJobTypes);
        Assert.Equal(OperationalJobTypes.ExecuteOrderFulfillment, enqueuedJobType);
    }
}
