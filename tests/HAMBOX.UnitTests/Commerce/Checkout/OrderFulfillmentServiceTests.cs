using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// H1 fix: <see cref="OrderFulfillmentService.FulfillMissingAsync"/> (the admin retry path) must
/// never fabricate a license key for a line item with no ProductVariantId — those are legacy
/// orders that predate the checkout-side fix, and there is no real deliverable behind them.
/// </summary>
public sealed class OrderFulfillmentServiceTests
{
    private static Order CreatePaidOrder(
        params (Guid ProductId, int Quantity, Guid? VariantId)[] items)
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
            items: items.Select(i => (i.ProductId, "Test Product", i.Quantity, 10m, i.VariantId, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)));

        order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        return order;
    }

    [Fact]
    public async Task FulfillMissingAsync_VariantBackedItem_DeliversRealInventoryCode()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var order = CreatePaidOrder((productId, 1, variantId));

        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variantId] = 1;
        inventoryEngine.CodesByVariant[variantId] = new Queue<string>(["REAL-GAME-KEY-0001"]);

        var service = new OrderFulfillmentService(
            commerceDb, inventoryEngine, new NullSupplierFulfillmentService(), new FakeFulfillmentRouter(),
            new NullSupplierPricingEngine(), HAMBOX.UnitTests.Suppliers.TestDoubles.SuppliersTestDbContextFactory.Create(),
            new HAMBOX.UnitTests.Commerce.TestDoubles.FakeDeliveryTicketService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HAMBOX.Modules.Commerce.Application.Services.OrderFulfillmentService>.Instance);
        var result = await service.FulfillMissingAsync(order, CancellationToken.None);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(1, result.CodesDelivered);

        var key = Assert.Single(commerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id));
        Assert.Equal("REAL-GAME-KEY-0001", key.LicenseKey);
        Assert.NotNull(key.DigitalInventoryCodeId);

        // A single-line order fully delivered in one call completes in that same call: the check counts the
        // keys already attached plus the ones just delivered (they are still unsaved), not a database query.
        Assert.True(result.OrderCompleted);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public async Task FulfillMissingAsync_InsufficientInventory_DeliversNothingWithoutThrowing()
    {
        // Phase 7 routing change: FulfillMissingAsync now reserves manual stock via the
        // never-throwing ReservePartialCodesAsync (0 up to the requested quantity) instead of the
        // all-or-nothing ReserveCodesAsync — a single short line no longer throws and blocks every
        // other line in the same order from being processed. Zero available stock now means zero
        // delivered, not an exception.
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var order = CreatePaidOrder((productId, 1, variantId));

        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variantId] = 0;

        var service = new OrderFulfillmentService(
            commerceDb, inventoryEngine, new NullSupplierFulfillmentService(), new FakeFulfillmentRouter(),
            new NullSupplierPricingEngine(), HAMBOX.UnitTests.Suppliers.TestDoubles.SuppliersTestDbContextFactory.Create(),
            new HAMBOX.UnitTests.Commerce.TestDoubles.FakeDeliveryTicketService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HAMBOX.Modules.Commerce.Application.Services.OrderFulfillmentService>.Instance);

        var result = await service.FulfillMissingAsync(order, CancellationToken.None);

        Assert.Equal(0, result.CodesDelivered);
        Assert.False(result.OrderCompleted);
        Assert.Empty(commerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id));
    }

    [Fact]
    public async Task FulfillMissingAsync_VariantLessLineItem_IsSkippedNotFabricated()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var productId = Guid.NewGuid();

        // Simulates a legacy order created before the H1 checkout fix, with a line item that
        // has no ProductVariantId — there is no real inventory-backed deliverable for it.
        var order = CreatePaidOrder((productId, 2, null));

        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var service = new OrderFulfillmentService(
            commerceDb, inventoryEngine, new NullSupplierFulfillmentService(), new FakeFulfillmentRouter(),
            new NullSupplierPricingEngine(), HAMBOX.UnitTests.Suppliers.TestDoubles.SuppliersTestDbContextFactory.Create(),
            new HAMBOX.UnitTests.Commerce.TestDoubles.FakeDeliveryTicketService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HAMBOX.Modules.Commerce.Application.Services.OrderFulfillmentService>.Instance);

        var result = await service.FulfillMissingAsync(order, CancellationToken.None);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(0, result.CodesDelivered);
        Assert.False(result.OrderCompleted);
        Assert.Empty(commerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id));
        Assert.NotEqual(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public async Task FulfillMissingAsync_MixedOrder_DeliversRealCodeButLeavesOrderProcessing_NeverFakesTheMissingLine()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var variantBackedProductId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var legacyProductId = Guid.NewGuid();

        var order = CreatePaidOrder(
            (variantBackedProductId, 1, variantId),
            (legacyProductId, 1, null));

        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variantId] = 1;
        inventoryEngine.CodesByVariant[variantId] = new Queue<string>(["REAL-GAME-KEY-0002"]);

        var service = new OrderFulfillmentService(
            commerceDb, inventoryEngine, new NullSupplierFulfillmentService(), new FakeFulfillmentRouter(),
            new NullSupplierPricingEngine(), HAMBOX.UnitTests.Suppliers.TestDoubles.SuppliersTestDbContextFactory.Create(),
            new HAMBOX.UnitTests.Commerce.TestDoubles.FakeDeliveryTicketService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HAMBOX.Modules.Commerce.Application.Services.OrderFulfillmentService>.Instance);
        var result = await service.FulfillMissingAsync(order, CancellationToken.None);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        // The variant-backed line was genuinely delivered...
        Assert.Equal(1, result.CodesDelivered);
        var deliveredKeys = commerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id).ToList();
        Assert.Single(deliveredKeys);
        Assert.Equal("REAL-GAME-KEY-0002", deliveredKeys[0].LicenseKey);

        // ...but the order is honestly left Processing, not silently completed — there is no
        // fabricated key standing in for the still-missing legacy line.
        Assert.False(result.OrderCompleted);
        Assert.Equal(OrderStatus.Processing, order.Status);
    }

    [Fact]
    public async Task FulfillMissingAsync_UnpaidOrder_ThrowsInvalidOperationException()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
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
            items: [(Guid.NewGuid(), "Test Product", 1, 10m, Guid.NewGuid(), (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        // Deliberately not calling RecordPayment — PaymentStatus stays unpaid.

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var service = new OrderFulfillmentService(
            commerceDb, inventoryEngine, new NullSupplierFulfillmentService(), new FakeFulfillmentRouter(),
            new NullSupplierPricingEngine(), HAMBOX.UnitTests.Suppliers.TestDoubles.SuppliersTestDbContextFactory.Create(),
            new HAMBOX.UnitTests.Commerce.TestDoubles.FakeDeliveryTicketService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HAMBOX.Modules.Commerce.Application.Services.OrderFulfillmentService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FulfillMissingAsync(order, CancellationToken.None));
    }
}
