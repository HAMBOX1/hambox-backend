using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.Cryptomus;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.RecordPaymentWebhook;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.OrderHold;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// Audit R21/R22/R25/R30: per-order and per-day purchase caps, manual-review / suspicious-activity holds that stop
/// automatic delivery until an admin releases them, paid orders consuming the stock held for them, and the webhook
/// audit rows (including callbacks that match no order).
/// </summary>
public sealed class PurchaseControlsAndOrderHoldTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private static Order NewOrder(
        string userId = "user-1",
        int quantity = 1,
        Guid? productId = null,
        Guid? variantId = null,
        bool paid = true)
    {
        var order = Order.Create(
            userId: userId,
            orderNumber: $"ORD-{Guid.NewGuid():N}",
            email: "buyer@example.com",
            country: "US",
            paymentMethod: "development",
            subtotal: 10m * quantity,
            discountAmount: 0m,
            taxAmount: 0m,
            totalAmount: 10m * quantity,
            items: [(productId ?? ProductId, "Product", quantity, 10m, variantId, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        if (paid)
        {
            order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        }

        return order;
    }

    /// <summary>The production <c>AuditInterceptor</c> stamps CreatedOnUtc; the in-memory test context has none, so do it here.</summary>
    private static async Task SaveOrdersAsync(CommerceDbContext db, params Order[] orders)
    {
        db.Orders.AddRange(orders);
        foreach (var order in orders)
        {
            db.Entry(order).Property(o => o.CreatedOnUtc).CurrentValue = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static Product NewProduct(int? perOrder = null, int? perDay = null, bool review = false)
    {
        var product = Product.Create("منتج", "Product", "d", "d", 10m, Guid.NewGuid());
        product.SetPurchaseControls(perOrder, perDay, review);
        return product;
    }

    private static CartItem Line(Guid productId, int quantity)
    {
        var cart = ShoppingCart.CreateForUser("cart-user");
        cart.AddOrUpdateItem(productId, quantity, 10m);
        return cart.Items.Single();
    }

    private static IReadOnlyDictionary<Guid, Product> Catalog(Product product) =>
        new Dictionary<Guid, Product> { [product.Id] = product };

    // ─── R22: caps ──────────────────────────────────────────────────────

    [Fact]
    public async Task PerOrderCap_RefusesAnOversizedLine_AndAcceptsOneWithinIt()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var product = NewProduct(perOrder: 3);
        var service = new PurchaseControlService(db);

        var tooMany = await service.CheckLimitsAsync("user-1", [Line(product.Id, 4)], Catalog(product), CancellationToken.None);
        Assert.Equal("Checkout.PurchaseLimitPerOrderExceeded", tooMany.Error.Code);

        var fine = await service.CheckLimitsAsync("user-1", [Line(product.Id, 3)], Catalog(product), CancellationToken.None);
        Assert.True(fine.IsSuccess);
    }

    [Fact]
    public async Task PerDayCap_CountsWhatTheCustomerAlreadyBoughtInTheLast24Hours()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var product = NewProduct(perDay: 3);
        await SaveOrdersAsync(db, NewOrder("user-1", quantity: 2, productId: product.Id));
        var service = new PurchaseControlService(db);

        var over = await service.CheckLimitsAsync("user-1", [Line(product.Id, 2)], Catalog(product), CancellationToken.None);
        Assert.Equal("Checkout.PurchaseLimitPerDayExceeded", over.Error.Code);

        var ok = await service.CheckLimitsAsync("user-1", [Line(product.Id, 1)], Catalog(product), CancellationToken.None);
        Assert.True(ok.IsSuccess);

        // Another customer is unaffected.
        var other = await service.CheckLimitsAsync("user-2", [Line(product.Id, 3)], Catalog(product), CancellationToken.None);
        Assert.True(other.IsSuccess);
    }

    [Fact]
    public async Task PerDayCap_IgnoresOldAndCancelledOrders()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var product = NewProduct(perDay: 2);

        var old = NewOrder("user-1", quantity: 2, productId: product.Id);
        var cancelled = NewOrder("user-1", quantity: 2, productId: product.Id, paid: false);
        cancelled.Cancel();
        await SaveOrdersAsync(db, old, cancelled);
        db.Entry(old).Property(o => o.CreatedOnUtc).CurrentValue = DateTimeOffset.UtcNow.AddHours(-30);
        await db.SaveChangesAsync();

        var result = await new PurchaseControlService(db)
            .CheckLimitsAsync("user-1", [Line(product.Id, 2)], Catalog(product), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // ─── R22: review flags ──────────────────────────────────────────────

    [Fact]
    public async Task SensitiveProduct_FlagsTheOrderForManualReview()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var product = NewProduct(review: true);
        var order = NewOrder(productId: product.Id);

        var reason = await new PurchaseControlService(db).ApplyAsync(order, Catalog(product), CancellationToken.None);

        Assert.StartsWith("Sensitive product", reason);
        Assert.Equal(OrderHoldKind.ManualReview, order.HoldKind);
        Assert.Equal("Pending Manual Review", AdminOrderMapper.ResolveOrderStatusLabel(order));
    }

    [Fact]
    public async Task ManyOrdersInAnHour_TripTheVelocityFlag_ButANormalCustomerIsNotFlagged()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var product = NewProduct();
        var seeded = Enumerable.Range(0, PurchaseControlService.VelocityOrdersPerHour - 1)
            .Select(_ => NewOrder("bot-1", productId: product.Id))
            .Append(NewOrder("calm-1", productId: product.Id))
            .ToArray();
        await SaveOrdersAsync(db, seeded);
        var service = new PurchaseControlService(db);

        var flagged = await service.ApplyAsync(NewOrder("bot-1", productId: product.Id), Catalog(product), CancellationToken.None);
        Assert.StartsWith("Suspicious activity", flagged);

        var normal = await service.ApplyAsync(NewOrder("calm-1", productId: product.Id), Catalog(product), CancellationToken.None);
        Assert.Null(normal);
    }

    [Fact]
    public async Task RepeatedFailedPayments_TripTheFailedPaymentFlag()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var product = NewProduct();
        var failedOrders = Enumerable.Range(0, PurchaseControlService.FailedPaymentsPerDay).Select(_ =>
        {
            var failed = NewOrder("card-tester", productId: product.Id, paid: false);
            failed.MarkFailed();
            return failed;
        }).ToArray();
        await SaveOrdersAsync(db, failedOrders);

        var reason = await new PurchaseControlService(db)
            .AssessAsync(NewOrder("card-tester", productId: product.Id, paid: false), Catalog(product), CancellationToken.None);

        Assert.Contains("failed payments", reason);
    }

    // ─── hold blocks delivery until released ────────────────────────────

    private static OrderFulfillmentService CreateFulfillment(
        CommerceDbContext commerceDb, FakeInventoryEngine inventory, FakeFulfillmentRouter? router = null) =>
        new(
            commerceDb, inventory, new NullSupplierFulfillmentService(), router ?? new FakeFulfillmentRouter(),
            new NullSupplierPricingEngine(),
            HAMBOX.UnitTests.Suppliers.TestDoubles.SuppliersTestDbContextFactory.Create(),
            new FakeDeliveryTicketService(),
            NullLogger<OrderFulfillmentService>.Instance);

    [Fact]
    public async Task HeldOrder_IsNotFulfilled_UntilAnAdminReleasesTheHold()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var variantId = Guid.NewGuid();
        var order = NewOrder(variantId: variantId);
        order.PlaceOnHold(OrderHoldKind.ManualReview, "Sensitive product: Test");
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[variantId] = 1;
        inventory.CodesByVariant[variantId] = new Queue<string>(["KEY-HELD-0001"]);
        var service = CreateFulfillment(commerceDb, inventory);

        var held = await service.FulfillMissingAsync(order, CancellationToken.None);
        Assert.Equal(0, held.CodesDelivered);
        Assert.False(held.OrderCompleted);
        Assert.Empty(commerceDb.OrderLicenseKeys.Local);
        Assert.Equal(1, inventory.AvailableStockByVariant[variantId]); // nothing was reserved either

        order.ReleaseHold("admin-1");
        var released = await service.FulfillMissingAsync(order, CancellationToken.None);
        Assert.Equal(1, released.CodesDelivered);
        Assert.True(released.OrderCompleted);
    }

    [Fact]
    public async Task PaidOrder_DeliversTheCodeHeldForIt_WithoutReservingAnotherOne()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var variantId = Guid.NewGuid();
        var order = NewOrder(variantId: variantId);
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[variantId] = 2;
        inventory.CodesByVariant[variantId] = new Queue<string>(["KEY-HELD-FOR-ORDER", "KEY-FOR-SOMEONE-ELSE"]);

        // Initiation held one unit while the customer was off paying.
        var held = await inventory.ReserveForOrderAsync(
            variantId, 1, order.UserId, order.Id, DateTimeOffset.UtcNow.AddMinutes(30));
        Assert.Single(held);
        Assert.Equal(1, inventory.AvailableStockByVariant[variantId]);

        var result = await CreateFulfillment(commerceDb, inventory).FulfillMissingAsync(order, CancellationToken.None);

        Assert.Equal(1, result.CodesDelivered);
        Assert.Equal("KEY-HELD-FOR-ORDER", Assert.Single(commerceDb.OrderLicenseKeys.Local).LicenseKey);
        Assert.Equal(1, inventory.AvailableStockByVariant[variantId]); // still one left for others
    }

    [Fact]
    public async Task StockHoldService_HoldsManualLinesOnly_AndReleasesWithAReason()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var manualVariant = Guid.NewGuid();
        var supplierVariant = Guid.NewGuid();
        var order = Order.Create(
            "user-1", $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", "dot", 20m, 0m, 0m, 20m,
            [
                (ProductId, "Manual", 1, 10m, (Guid?)manualVariant, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null),
                (Guid.NewGuid(), "Supplier", 1, 10m, (Guid?)supplierVariant, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null),
            ]);

        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[manualVariant] = 1;
        inventory.AvailableStockByVariant[supplierVariant] = 1;
        var router = new FakeFulfillmentRouter();
        router.SetReadiness(supplierVariant, new(HAMBOX.Modules.Catalog.Domain.Enums.FulfillmentMode.SupplierOnly, false, null));

        var service = new OrderStockHoldService(inventory, catalogDb, router, NullLogger<OrderStockHoldService>.Instance);

        var held = await service.HoldAsync(order, DateTimeOffset.UtcNow.AddMinutes(30), CancellationToken.None);
        Assert.Equal(1, held);
        Assert.Equal(0, inventory.AvailableStockByVariant[manualVariant]);
        Assert.Equal(1, inventory.AvailableStockByVariant[supplierVariant]); // supplier-only line is never held

        await service.ReleaseAsync(order.Id, "Payment failed", CancellationToken.None);
        Assert.Contains((order.Id, "Payment failed"), inventory.OrderReleases);
    }

    // ─── admin hold / release ───────────────────────────────────────────

    [Fact]
    public async Task AdminHold_RequiresAReason_AuditsIt_AndReleaseQueuesFulfillment()
    {
        var (commerceDb, _) = CommerceTestDbContextFactory.Create();
        var order = NewOrder();
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();
        var admin = new FakeCurrentUserService("admin-1", "Admin One");
        var queue = new FakeOperationalJobQueue();

        var hold = new HoldAdminOrderCommandHandler(commerceDb, admin);
        var noReason = await hold.Handle(new HoldAdminOrderCommand(order.Id, "  "), CancellationToken.None);
        Assert.Equal(CommerceErrors.HoldReasonRequired.Code, noReason.Error.Code);

        var ok = await hold.Handle(new HoldAdminOrderCommand(order.Id, "Chargeback risk"), CancellationToken.None);
        Assert.True(ok.IsSuccess);
        Assert.Equal(OrderHoldKind.AdminHold, order.HoldKind);
        Assert.Equal("On Hold", AdminOrderMapper.ResolveOrderStatusLabel(order));
        Assert.Contains(commerceDb.OrderAuditEntries, e => e.EventType == "OrderHeld" && e.ActorUserId == "admin-1");

        var release = new ReleaseAdminOrderHoldCommandHandler(commerceDb, admin, queue);
        var released = await release.Handle(new ReleaseAdminOrderHoldCommand(order.Id, "Verified by phone"), CancellationToken.None);
        Assert.True(released.IsSuccess);
        Assert.Equal(OrderHoldKind.None, order.HoldKind);
        Assert.Equal("admin-1", order.HoldReleasedByUserId);
        Assert.Contains(OperationalJobTypes.ExecuteOrderFulfillment, queue.EnqueuedJobTypes);

        var again = await release.Handle(new ReleaseAdminOrderHoldCommand(order.Id, null), CancellationToken.None);
        Assert.Equal(CommerceErrors.OrderNotOnHold.Code, again.Error.Code);
    }

    // ─── R30: webhook audit ─────────────────────────────────────────────

    [Fact]
    public async Task CallbackForAnUnknownOrder_IsAckedAndLoggedAsOrderNotFound()
    {
        var (commerceDb, _) = CommerceTestDbContextFactory.Create();
        var handler = new HandleCryptomusWebhookCommandHandler(
            commerceDb, verificationService: null!, NullLogger<HandleCryptomusWebhookCommandHandler>.Instance);

        var result = await handler.Handle(
            new HandleCryptomusWebhookCommand("no-such-order", "uuid-123", "paid", true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(commerceDb.PaymentWebhookLogs);
        Assert.Equal("Cryptomus", row.Provider);
        Assert.Equal("OrderNotFound", row.ProcessingResult);
        Assert.Equal("Valid", row.SignatureVerificationResult);
        Assert.Equal("uuid-123", row.GatewayReference);
        Assert.Equal("no-such-order", row.MerchantReference);
        Assert.Contains("no-such-order", row.RawPayload);
    }

    [Fact]
    public async Task RejectedCallback_IsRecordedWithItsSignatureResult_AndAnOversizedBodyIsTruncated()
    {
        var (commerceDb, _) = CommerceTestDbContextFactory.Create();
        var handler = new RecordPaymentWebhookCommandHandler(commerceDb);

        await handler.Handle(
            new RecordPaymentWebhookCommand(
                "OxaPay", "Invalid", "RejectedSignature", null, null, new string('x', 20_000), "203.0.113.9"),
            CancellationToken.None);

        var row = Assert.Single(commerceDb.PaymentWebhookLogs);
        Assert.Equal("Invalid", row.SignatureVerificationResult);
        Assert.Equal("RejectedSignature", row.ProcessingResult);
        Assert.Equal("203.0.113.9", row.SourceIp);
        Assert.Equal(PaymentWebhookLog.MaxRawPayloadLength, row.RawPayload.Length);
    }

    // ─── R25: lifecycle stage ───────────────────────────────────────────

    [Fact]
    public void FulfillmentStage_FollowsTheScopeLifecycle()
    {
        var unpaid = NewOrder(paid: false);
        Assert.Equal("Created", AdminOrderMapper.ResolveFulfillmentStage(unpaid, 0, false, false));

        var paid = NewOrder();
        Assert.Equal("Payment Confirmed", AdminOrderMapper.ResolveFulfillmentStage(paid, 0, false, false));
        Assert.Equal("Queued", AdminOrderMapper.ResolveFulfillmentStage(paid, 0, hasQueuedJob: true, hasRetryingJob: false));
        Assert.Equal("API Retry", AdminOrderMapper.ResolveFulfillmentStage(paid, 0, hasQueuedJob: false, hasRetryingJob: true));
        Assert.Equal("Partially Delivered", AdminOrderMapper.ResolveFulfillmentStage(paid, 1, false, false));

        var review = NewOrder();
        review.PlaceOnHold(OrderHoldKind.ManualReview, "Sensitive product");
        Assert.Equal("Pending Manual Review", AdminOrderMapper.ResolveFulfillmentStage(review, 0, true, false));

        var held = NewOrder();
        held.PlaceOnHold(OrderHoldKind.AdminHold, "Chargeback risk");
        Assert.Equal("On Hold", AdminOrderMapper.ResolveFulfillmentStage(held, 0, false, false));

        var done = NewOrder();
        done.Complete();
        Assert.Equal("Completed", AdminOrderMapper.ResolveFulfillmentStage(done, 1, false, false));
    }
}
