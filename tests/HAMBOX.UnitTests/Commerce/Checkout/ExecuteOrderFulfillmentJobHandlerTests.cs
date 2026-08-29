using HAMBOX.Infrastructure.Currency;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;
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
/// Covers <see cref="ExecuteOrderFulfillmentJobHandler"/> — the Job/Worker counterpart to what
/// checkout used to call inline. Wires the exact same real production chain
/// (<c>OrderFulfillmentService</c> → real <c>SupplierFulfillmentService</c> → real
/// <c>CommerceOrderLicenseKeyDeliverySink</c>) <see cref="OrderFulfillmentServiceAutomatedSupplierTests"/>
/// already proves correct for the underlying method — this file proves the JOB HANDLER wrapper around
/// it: it delegates unmodified, detects completion correctly, and calling it twice for the same order
/// never results in two supplier purchases.
/// </summary>
public sealed class ExecuteOrderFulfillmentJobHandlerTests
{
    private static Order CreateOrder(Guid productId, Guid variantId, int quantity)
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

        order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        order.MarkProcessing(); // mirrors checkout's own fix: Paid but not yet Completed when a shortfall exists

        return order;
    }

    private static async Task<Guid> SeedVariantAsync(
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb, Guid productId)
    {
        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        variant.SetFulfillmentMode(FulfillmentMode.SupplierOnly);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();
        return variant.Id;
    }

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

    private sealed record Harness(
        ExecuteOrderFulfillmentJobHandler Handler,
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext CommerceDb,
        FakeSupplierProvider Provider);

    private static async Task<(Harness Harness, Order Order)> CreateHarnessAsync()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();

        var provider = new FakeSupplierProvider("Fake");
        var registry = new SupplierProviderRegistry([provider]);
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var deliverySink = new CommerceOrderLicenseKeyDeliverySink(
            commerceDb, catalogDb, inventoryEngine, new FakeCommerceTransactionService(),
            NullLogger<CommerceOrderLicenseKeyDeliverySink>.Instance);
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
        var pricingEngine = new SupplierPricingEngine(
            routingEngine, new FakeCommerceSettingsProvider { Commerce = new(0m, false, 15, 24, 14, "INV-", DefaultSupplierMarginPercent: 0m) });

        var fulfillmentService = new OrderFulfillmentService(
            commerceDb, inventoryEngine, supplierFulfillmentService, router, pricingEngine, suppliersDb, NullLogger<OrderFulfillmentService>.Instance);

        var referralRewardService = new ReferralRewardService(commerceDb, new FakeMembershipEngine());
        var referralLifecycle = new ReferralLifecycleService(
            commerceDb, new FakeCommerceSettingsProvider(), referralRewardService, new FakeCommunicationService(),
            NullLogger<ReferralLifecycleService>.Instance);

        var handler = new ExecuteOrderFulfillmentJobHandler(
            new FakeBackgroundJobSerializer(), commerceDb, fulfillmentService, referralLifecycle);

        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId);
        await SeedMappedSupplierAsync(suppliersDb, productId);

        var order = CreateOrder(productId, variantId, quantity: 1);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        return (new Harness(handler, commerceDb, provider), order);
    }

    [Fact]
    public async Task HandleAsync_DelegatesToOrderFulfillmentService_AndCompletesTheOrderOnSuccess()
    {
        var (harness, order) = await CreateHarnessAsync();
        var context = new FakeBackgroundJobContext { RelatedEntityType = "Order", RelatedEntityId = order.Id.ToString() };

        await harness.Handler.HandleAsync(null, context, CancellationToken.None);

        var persistedOrder = harness.CommerceDb.Orders.AsNoTracking().Single(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Completed, persistedOrder.Status);
        Assert.Single(await harness.CommerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id).ToListAsync());
        Assert.Single(harness.Provider.PurchaseCalls);
    }

    /// <summary>
    /// The exact scenario Phase 6 calls out: the worker (or an operator) runs this job twice for the
    /// same order — a retry after an ambiguous outcome, a stray duplicate enqueue, whatever the cause.
    /// The SECOND run must never place a second supplier purchase — SupplierFulfillmentService's own
    /// idempotency (reusing the open, non-terminal attempt for this exact scope) must hold through the
    /// job handler exactly as it does when called directly.
    /// </summary>
    [Fact]
    public async Task HandleAsync_CalledTwiceForTheSameOrder_NeverPlacesASecondSupplierPurchase()
    {
        var (harness, order) = await CreateHarnessAsync();
        var context = new FakeBackgroundJobContext { RelatedEntityType = "Order", RelatedEntityId = order.Id.ToString() };

        await harness.Handler.HandleAsync(null, context, CancellationToken.None);
        await harness.Handler.HandleAsync(null, context, CancellationToken.None);

        Assert.Single(harness.Provider.PurchaseCalls);
        Assert.Single(await harness.CommerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id).ToListAsync());

        var persistedOrder = harness.CommerceDb.Orders.AsNoTracking().Single(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Completed, persistedOrder.Status);
    }

    [Fact]
    public async Task HandleAsync_AmbiguousProviderOutcome_LeavesOrderProcessing_NeverCompletesOrFailsPrematurely()
    {
        var (harness, order) = await CreateHarnessAsync();
        harness.Provider.PurchaseThrows = (_, _) => new TimeoutException("simulated ambiguous timeout");
        var context = new FakeBackgroundJobContext { RelatedEntityType = "Order", RelatedEntityId = order.Id.ToString() };

        await harness.Handler.HandleAsync(null, context, CancellationToken.None);

        var persistedOrder = harness.CommerceDb.Orders.AsNoTracking().Single(o => o.Id == order.Id);
        // Detection and failure are separate concepts — an ambiguous supplier outcome must never mark
        // the order Completed (nothing was delivered) or Failed (it may still resolve) — it stays
        // Processing, exactly where checkout's own fix left it, until reconciliation resolves it.
        Assert.Equal(OrderStatus.Processing, persistedOrder.Status);
        Assert.Empty(await harness.CommerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task HandleAsync_MissingOrderId_ThrowsRecoverably()
    {
        var (harness, _) = await CreateHarnessAsync();
        var context = new FakeBackgroundJobContext(); // no RelatedEntityId, no payload

        // Throwing (rather than silently no-op-ing) is what makes OperationalJobWorker's existing
        // retry/dead-letter machinery apply to this job type — a job with no resolvable order id is a
        // real defect, not a benign no-op.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Handler.HandleAsync(null, context, CancellationToken.None));
    }
}
