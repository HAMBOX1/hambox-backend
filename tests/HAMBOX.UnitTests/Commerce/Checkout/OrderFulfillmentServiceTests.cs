using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;

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
            items: items.Select(i => (i.ProductId, "Test Product", i.Quantity, 10m, i.VariantId, (string?)null)));

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

        var service = new OrderFulfillmentService(commerceDb, inventoryEngine);
        var result = await service.FulfillMissingAsync(order, CancellationToken.None);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(1, result.CodesDelivered);

        var key = Assert.Single(commerceDb.OrderLicenseKeys.Where(k => k.OrderId == order.Id));
        Assert.Equal("REAL-GAME-KEY-0001", key.LicenseKey);
        Assert.NotNull(key.DigitalInventoryCodeId);

        // Pre-existing behavior, unrelated to H1: FulfillMissingAsync's own "is the order now
        // fully delivered" check queries OrderLicenseKeys from the db context *before* this
        // call's own SaveChangesAsync (that only happens in the caller, after this method
        // returns), so it never sees the key(s) it just added in the same call. A single-line
        // order fully delivered in one retry call is therefore left Processing, not Completed,
        // by this method — verified here, not fixed, since it's outside H1's scope.
        Assert.False(result.OrderCompleted);
        Assert.Equal(OrderStatus.Processing, order.Status);
    }

    [Fact]
    public async Task FulfillMissingAsync_InsufficientInventory_ThrowsAndDeliversNothing()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var order = CreatePaidOrder((productId, 1, variantId));

        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variantId] = 0;

        var service = new OrderFulfillmentService(commerceDb, inventoryEngine);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FulfillMissingAsync(order, CancellationToken.None));

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
        var service = new OrderFulfillmentService(commerceDb, inventoryEngine);

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

        var service = new OrderFulfillmentService(commerceDb, inventoryEngine);
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
            items: [(Guid.NewGuid(), "Test Product", 1, 10m, Guid.NewGuid(), (string?)null)]);
        // Deliberately not calling RecordPayment — PaymentStatus stays unpaid.

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var service = new OrderFulfillmentService(commerceDb, inventoryEngine);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FulfillMissingAsync(order, CancellationToken.None));
    }
}
