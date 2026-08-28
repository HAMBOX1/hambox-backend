using HAMBOX.Infrastructure.Currency;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using HAMBOX.Modules.Suppliers.Application.Options;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// Covers <see cref="OrderFulfillmentService.QueueAutomatedSupplierFulfillmentAsync"/> — the seam that
/// connects a paid order's remaining shortfall to the generic Suppliers module. Uses a real
/// <c>SupplierFulfillmentService</c> + <c>FakeSupplierProvider</c> (not a null double) so these tests
/// actually exercise supplier-selection and purchase-call behavior, not just Commerce-side plumbing.
/// Every order item carries a real <see cref="ProductVariant"/> (seeded with
/// <see cref="FulfillmentMode.ManualFirst"/> unless a test says otherwise) — routing now decides
/// per-variant, so a variant-less line item is deliberately skipped rather than routed (mirrors
/// <c>FulfillMissingAsync</c>'s existing "no real deliverable" skip for legacy variant-less lines).
/// </summary>
public sealed class OrderFulfillmentServiceAutomatedSupplierTests
{
    private static Order CreateOrder(Guid productId, Guid variantId, int quantity, bool paid)
    {
        var order = Order.Create(
            userId: "user-1",
            orderNumber: $"ORD-{Guid.NewGuid():N}",
            email: "buyer@example.com",
            country: "US",
            paymentMethod: "development",
            subtotal: 10m,
            discountAmount: 0m,
            taxAmount: 0m,
            totalAmount: 10m,
            items: [(productId, "Test Product", quantity, 10m, (Guid?)variantId, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);

        if (paid)
        {
            order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        }

        return order;
    }

    /// <summary>
    /// One provider per requested type (default: a single "Fake" provider, for every existing
    /// single-supplier test in this file) — <paramref name="providerTypes"/> lets failover/multi-supplier
    /// tests seed several distinctly-behaving <see cref="FakeSupplierProvider"/> instances at once.
    /// </summary>
    private static (OrderFulfillmentService Service, FakeSupplierProvider Provider, IReadOnlyDictionary<string, FakeSupplierProvider> Providers, HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext SuppliersDb, HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext CommerceDb, HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext CatalogDb)
        CreateHarness(params string[] providerTypes)
    {
        var types = providerTypes.Length == 0 ? ["Fake"] : providerTypes;
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var providers = types.Select(t => new FakeSupplierProvider(t)).ToArray();
        var provider = providers[0];
        var registry = new SupplierProviderRegistry(providers);
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var deliverySink = new HAMBOX.Modules.Commerce.Application.Services.CommerceOrderLicenseKeyDeliverySink(
            commerceDb, catalogDb, inventoryEngine, new FakeCommerceTransactionService(),
            NullLogger<HAMBOX.Modules.Commerce.Application.Services.CommerceOrderLicenseKeyDeliverySink>.Instance);
        var supplierFulfillmentService = new HAMBOX.Modules.Suppliers.Application.Services.SupplierFulfillmentService(
            suppliersDb, registry, NullLogger<HAMBOX.Modules.Suppliers.Application.Services.SupplierFulfillmentService>.Instance, deliverySink);

        var router = new FulfillmentRouter(catalogDb, suppliersDb, registry, Options.Create(new SupplierAvailabilityOptions()));

        var exchangeRateService = new CurrencyExchangeRateService(
            new FakeCurrencyExchangeRateProvider(),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CurrencySettings()),
            TimeProvider.System,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
        var routingEngine = new SupplierRoutingEngine(suppliersDb, registry, exchangeRateService, Options.Create(new SupplierAvailabilityOptions()));
        var pricingEngine = new SupplierPricingEngine(routingEngine, new FakeCommerceSettingsProvider { Commerce = new(0m, false, 15, 24, 14, "INV-", DefaultSupplierMarginPercent: 0m) });

        var service = new OrderFulfillmentService(
            commerceDb, inventoryEngine, supplierFulfillmentService, router, pricingEngine, suppliersDb, NullLogger<OrderFulfillmentService>.Instance);

        return (service, provider, providers.ToDictionary(p => p.ProviderType), suppliersDb, commerceDb, catalogDb);
    }

    /// <summary>Seeds a real, active ProductVariant so the router has something to resolve a mode from.</summary>
    private static async Task<Guid> SeedVariantAsync(
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb,
        Guid productId,
        FulfillmentMode mode = FulfillmentMode.ManualFirst)
    {
        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        variant.SetFulfillmentMode(mode);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();
        return variant.Id;
    }

    /// <summary>
    /// Also seeds a fresh <c>Available</c> <see cref="SupplierProductAvailability"/> row — since the
    /// Supplier Availability phase, READY alone no longer makes <see cref="FulfillmentRouter"/> return a
    /// candidate; every test in this file that expects the provider to actually be called relies on
    /// this default.
    /// </summary>
    private static async Task<(Supplier Supplier, SupplierProductMapping Mapping)> SeedMappedSupplierAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext suppliersDb, Guid productId)
    {
        var supplier = Supplier.Create("Fake Supplier", $"SUP-{Guid.NewGuid():N}", "Fake", SupplierAuthenticationType.None, null, 0);
        suppliersDb.Suppliers.Add(supplier);
        var mapping = SupplierProductMapping.Create(supplier.Id, productId, "EXT-1", null, null, 5m, "USD", 0);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync();

        var availability = SupplierProductAvailability.CreateUnknown(supplier.Id, mapping.Id, "EXT-1");
        availability.RecordChecked(SupplierAvailabilityState.Available, null, DateTimeOffset.UtcNow, "EXT-1");
        suppliersDb.SupplierProductAvailabilities.Add(availability);
        await suppliersDb.SaveChangesAsync();

        return (supplier, mapping);
    }

    /// <summary>Seeds one supplier of a given (already-registered) provider type at a specific price — for multi-supplier/failover tests.</summary>
    private static async Task<(Supplier Supplier, SupplierProductMapping Mapping)> SeedPricedSupplierAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext suppliersDb,
        Guid productId, string providerType, decimal buyingPrice, string externalProductId)
    {
        var supplier = Supplier.Create(providerType, $"SUP-{Guid.NewGuid():N}", providerType, SupplierAuthenticationType.None, null, 0);
        suppliersDb.Suppliers.Add(supplier);
        var mapping = SupplierProductMapping.Create(supplier.Id, productId, externalProductId, null, null, buyingPrice, "USD", 0);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync();

        var availability = SupplierProductAvailability.CreateUnknown(supplier.Id, mapping.Id, externalProductId);
        availability.RecordChecked(SupplierAvailabilityState.Available, null, DateTimeOffset.UtcNow, externalProductId);
        suppliersDb.SupplierProductAvailabilities.Add(availability);
        await suppliersDb.SaveChangesAsync();

        return (supplier, mapping);
    }

    // 10. Cheapest supplier definitive failure -> fallback to the next cheapest.
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_CheapestDefinitiveFailure_FallsBackToNextCheapest()
    {
        var (service, _, providers, suppliersDb, commerceDb, catalogDb) = CreateHarness("Visoria", "Bamboo");
        var productId = Guid.NewGuid();
        await SeedPricedSupplierAsync(suppliersDb, productId, "Visoria", 7.45m, "EXT-VISORIA");
        await SeedPricedSupplierAsync(suppliersDb, productId, "Bamboo", 7.80m, "EXT-BAMBOO");
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 1, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        providers["Visoria"].PurchaseResponder = (_, _) =>
            new HAMBOX.Modules.Suppliers.Application.Abstractions.SupplierPurchaseResult(
                false, null, null, HAMBOX.Modules.Suppliers.Domain.Fulfillments.SupplierFulfillmentFailureCategory.ProductUnavailable, "out of stock");

        var summary = await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(1, summary.Queued);
        Assert.Single(providers["Visoria"].PurchaseCalls); // definitively rejected — tried
        Assert.Single(providers["Bamboo"].PurchaseCalls); // failover — next cheapest tried, and succeeded
        Assert.Equal(1, await commerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));

        var bambooFulfillment = await suppliersDb.SupplierFulfillments.FirstAsync(f => f.OrderId == order.Id && f.SupplierId == providers["Bamboo"].PurchaseCalls[0].SupplierId);
        Assert.Equal(HAMBOX.Modules.Suppliers.Domain.Fulfillments.SupplierFulfillmentStatus.Succeeded, bambooFulfillment.Status);
    }

    // 11. Cheapest supplier ambiguous failure -> NO fallback; the existing sweep reconciles it instead.
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_CheapestAmbiguousFailure_NeverFallsOver()
    {
        var (service, _, providers, suppliersDb, commerceDb, catalogDb) = CreateHarness("Visoria", "Bamboo");
        var productId = Guid.NewGuid();
        await SeedPricedSupplierAsync(suppliersDb, productId, "Visoria", 7.45m, "EXT-VISORIA");
        await SeedPricedSupplierAsync(suppliersDb, productId, "Bamboo", 7.80m, "EXT-BAMBOO");
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 1, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        // Simulates a timeout/connection-failure-after-submission — the documented "ambiguous, never
        // guess" case every real provider (GlobeTopper especially, having no idempotency) relies on.
        providers["Visoria"].PurchaseThrows = (_, _) => new TimeoutException("simulated ambiguous timeout");

        var summary = await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Single(providers["Visoria"].PurchaseCalls);
        Assert.Empty(providers["Bamboo"].PurchaseCalls); // never attempted — ambiguous means stop, not failover
        Assert.Equal(0, await commerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));

        var fulfillment = await suppliersDb.SupplierFulfillments.SingleAsync(f => f.OrderId == order.Id);
        Assert.Equal(HAMBOX.Modules.Suppliers.Domain.Fulfillments.SupplierFulfillmentStatus.Unknown, fulfillment.Status);
    }

    // 12. Successful (cheapest) supplier -> no other supplier is ever called.
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_CheapestSucceeds_NoOtherSupplierCalled()
    {
        var (service, _, providers, suppliersDb, commerceDb, catalogDb) = CreateHarness("Visoria", "Bamboo");
        var productId = Guid.NewGuid();
        await SeedPricedSupplierAsync(suppliersDb, productId, "Visoria", 7.45m, "EXT-VISORIA");
        await SeedPricedSupplierAsync(suppliersDb, productId, "Bamboo", 7.80m, "EXT-BAMBOO");
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 1, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        var summary = await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(1, summary.Queued);
        Assert.Single(providers["Visoria"].PurchaseCalls);
        Assert.Empty(providers["Bamboo"].PurchaseCalls);
    }

    // 14. Reconciliation resume (a previously-ambiguous attempt against the SAME candidate) -> never a second purchase call.
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_ResumingAnAmbiguousAttempt_NeverRepurchases_OnlyReconciles()
    {
        var (service, provider, _, suppliersDb, commerceDb, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        await SeedMappedSupplierAsync(suppliersDb, productId);
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 1, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        // First call: purchase call throws (ambiguous) -> fulfillment left Unknown, exactly one purchase call made.
        provider.PurchaseThrows = (_, _) => new TimeoutException("simulated ambiguous timeout");
        await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);
        Assert.Single(provider.PurchaseCalls);
        Assert.Equal(HAMBOX.Modules.Suppliers.Domain.Fulfillments.SupplierFulfillmentStatus.Unknown, (await suppliersDb.SupplierFulfillments.SingleAsync(f => f.OrderId == order.Id)).Status);

        // Second call (e.g. an admin retry, or a duplicate trigger) for the SAME order/candidate: the
        // resumed row must only ever be reconciled (status lookup), never re-submitted as a new purchase.
        provider.PurchaseThrows = null;
        provider.StatusResponder = (_, _) => new HAMBOX.Modules.Suppliers.Application.Abstractions.SupplierOrderStatusResult(
            HAMBOX.Modules.Suppliers.Application.Abstractions.SupplierProviderOrderStatus.Succeeded, "PROV-1", ["CODE-1"], null, null);
        await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Single(provider.PurchaseCalls); // still exactly one — never repurchased
        Assert.Single(provider.StatusCalls);
        Assert.Equal(1, await suppliersDb.SupplierFulfillments.CountAsync(f => f.OrderId == order.Id)); // still one row, now resolved
        Assert.Equal(HAMBOX.Modules.Suppliers.Domain.Fulfillments.SupplierFulfillmentStatus.Succeeded, (await suppliersDb.SupplierFulfillments.SingleAsync(f => f.OrderId == order.Id)).Status);
        Assert.Equal(1, await commerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));
    }

    // 15. A definite Succeeded outcome results in exactly one SupplierFulfillment row and one delivered code — no duplicate.
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_Success_ResultsInExactlyOneFulfillmentAndOneCode()
    {
        var (service, provider, _, suppliersDb, commerceDb, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        await SeedMappedSupplierAsync(suppliersDb, productId);
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 1, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Single(provider.PurchaseCalls);
        Assert.Equal(1, await suppliersDb.SupplierFulfillments.CountAsync(f => f.OrderId == order.Id));
        Assert.Equal(1, await commerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));
    }

    // 16. Customer selling price is never touched by routing (routing only ever reads/writes Suppliers-schema data and the admin-only audit log).
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_NeverChangesTheOrderItemsSellingPrice()
    {
        var (service, _, _, suppliersDb, commerceDb, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        await SeedMappedSupplierAsync(suppliersDb, productId); // BuyingPrice 5m — well below the $10 selling price below
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 1, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();
        var originalUnitPrice = order.Items.Single().UnitPrice;
        var originalTotal = order.TotalAmount;

        await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(originalUnitPrice, order.Items.Single().UnitPrice);
        Assert.Equal(originalTotal, order.TotalAmount);
        Assert.Equal(10m, order.Items.Single().UnitPrice); // the $10 selling price from CreateOrder, unaffected by the $5 supplier cost
    }

    // 17. Supplier acquisition cost is written only to the Suppliers-schema audit log, never onto anything Commerce exposes to a customer.
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_RecordsCostOnlyInTheAdminAuditLog()
    {
        var (service, _, _, suppliersDb, commerceDb, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        await SeedMappedSupplierAsync(suppliersDb, productId); // BuyingPrice 5m
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 1, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        var auditLog = await suppliersDb.SupplierRoutingAuditLogs.SingleAsync(l => l.OrderId == order.Id);
        Assert.Equal(5m, auditLog.SelectedCostInBaseCurrency);
    }

    // P. Payment not completed -> the automated supplier is NEVER called.
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_UnpaidOrder_NeverCallsProvider()
    {
        var (service, provider, _, suppliersDb, _, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        await SeedMappedSupplierAsync(suppliersDb, productId);
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 2, paid: false);

        var summary = await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(0, summary.ShortfallLines);
        Assert.Equal(0, summary.Queued);
        Assert.Empty(provider.PurchaseCalls);
        Assert.False(await suppliersDb.SupplierFulfillments.AnyAsync());
    }

    // Q. Successful payment -> a fulfillment is created (and processed) exactly once, and the delivered
    // code is actually attached to the order (OrderLicenseKey) — not just recorded in Suppliers.
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_PaidOrderWithShortfall_CreatesExactlyOneFulfillment()
    {
        var (service, provider, _, suppliersDb, commerceDb, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        await SeedMappedSupplierAsync(suppliersDb, productId);
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 2, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        var summary = await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(1, summary.ShortfallLines);
        Assert.Equal(1, summary.Queued);
        Assert.Single(provider.PurchaseCalls);
        Assert.Equal(1, await suppliersDb.SupplierFulfillments.CountAsync(f => f.OrderId == order.Id));

        var fulfillment = await suppliersDb.SupplierFulfillments.FirstAsync(f => f.OrderId == order.Id);
        Assert.Equal(2, fulfillment.RequestedQuantity); // exactly the shortfall, never more
        Assert.Equal(2, await commerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));
    }

    // R. Duplicate payment callback/event calling this twice must never create a duplicate purchase —
    // this is the scenario that originally caught a real bug (codes weren't being attached back to the
    // order, so a second call saw no OrderLicenseKeys and recomputed the same shortfall).
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_CalledTwice_ForSameOrder_NeverDuplicatesThePurchase()
    {
        var (service, provider, _, suppliersDb, commerceDb, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        await SeedMappedSupplierAsync(suppliersDb, productId);
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 2, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);
        // Simulates a duplicate payment-confirmation callback re-triggering the same fulfillment step.
        await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(1, await suppliersDb.SupplierFulfillments.CountAsync(f => f.OrderId == order.Id));
        Assert.Single(provider.PurchaseCalls);
        Assert.Equal(2, await commerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));
    }

    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_NoManualShortfall_NeverCreatesAFulfillment()
    {
        var (service, provider, _, suppliersDb, commerceDb, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        await SeedMappedSupplierAsync(suppliersDb, productId);
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 1, paid: true);
        commerceDb.Orders.Add(order);
        // A manual OrderLicenseKey already covers the full quantity — nothing for the automated path to do.
        commerceDb.OrderLicenseKeys.Add(HAMBOX.Modules.Commerce.Domain.Account.OrderLicenseKey.Create(
            order.Id, order.Items.First().Id, productId, "MANUAL-CODE-1"));
        await commerceDb.SaveChangesAsync();

        var summary = await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(0, summary.ShortfallLines);
        Assert.Empty(provider.PurchaseCalls);
    }

    // S. Product-scoped supplier resolution never crosses into another product's mapping (IDOR-shaped correctness).
    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_OnlyUsesTheMappingForThisExactProduct()
    {
        var (service, provider, _, suppliersDb, commerceDb, catalogDb) = CreateHarness();
        var targetProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();

        var (_, targetMapping) = await SeedMappedSupplierAsync(suppliersDb, targetProductId);
        await SeedMappedSupplierAsync(suppliersDb, otherProductId); // an unrelated product's own mapping
        var variantId = await SeedVariantAsync(catalogDb, targetProductId);

        var order = CreateOrder(targetProductId, variantId, 1, paid: true);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        var fulfillment = await suppliersDb.SupplierFulfillments.SingleAsync(f => f.OrderId == order.Id);
        Assert.Equal(targetMapping.Id, fulfillment.SupplierProductMappingId);
    }

    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_NoMappingForProduct_LeavesOrderUntouched()
    {
        var (service, provider, _, suppliersDb, _, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid(); // no mapping seeded at all
        var variantId = await SeedVariantAsync(catalogDb, productId);
        var order = CreateOrder(productId, variantId, 1, paid: true);

        var summary = await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(1, summary.ShortfallLines);
        Assert.Equal(1, summary.NoSupplierAvailable);
        Assert.Empty(provider.PurchaseCalls);
    }

    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_DisabledSupplier_IsNeverSelected()
    {
        var (service, provider, _, suppliersDb, _, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        var (supplier, _) = await SeedMappedSupplierAsync(suppliersDb, productId);
        var tracked = await suppliersDb.Suppliers.FirstAsync(s => s.Id == supplier.Id);
        tracked.Disable();
        await suppliersDb.SaveChangesAsync();
        var variantId = await SeedVariantAsync(catalogDb, productId);

        var order = CreateOrder(productId, variantId, 1, paid: true);
        var summary = await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(1, summary.NoSupplierAvailable);
        Assert.Empty(provider.PurchaseCalls);
    }

    [Fact]
    public async Task QueueAutomatedSupplierFulfillmentAsync_ManualOnlyVariant_NeverCallsProvider()
    {
        var (service, provider, _, suppliersDb, _, catalogDb) = CreateHarness();
        var productId = Guid.NewGuid();
        await SeedMappedSupplierAsync(suppliersDb, productId);
        // A READY supplier exists, but the variant explicitly opts out of ever using it.
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.ManualOnly);
        var order = CreateOrder(productId, variantId, 1, paid: true);

        var summary = await service.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(0, summary.ShortfallLines);
        Assert.Empty(provider.PurchaseCalls);
        Assert.False(await suppliersDb.SupplierFulfillments.AnyAsync());
    }
}
