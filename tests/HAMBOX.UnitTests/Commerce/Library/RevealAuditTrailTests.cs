using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Account.Library;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.RevealAdminOrderLicenseKey;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Library;

/// <summary>
/// Audit R19/R20/R13: every plaintext reveal of an order license key — by an admin or by the customer — leaves an
/// audit row with who, which key, why (admin), from which IP and device, and (customer) whether an e-mailed code
/// was verified; and fulfillment snapshots the source mode on the order line.
/// </summary>
public sealed class RevealAuditTrailTests
{
    private const string Plaintext = "REAL-SECRET-CODE-XYZ-0001";

    private static async Task<(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext Db,
        Order Order,
        OrderLicenseKey Key)> SeedAsync(string owner = "user-1")
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var productId = Guid.NewGuid();
        var order = Order.Create(
            userId: owner,
            orderNumber: $"ORD-{Guid.NewGuid():N}",
            email: "buyer@example.com",
            country: "US",
            paymentMethod: "development",
            subtotal: 19.99m,
            discountAmount: 0m,
            taxAmount: 0m,
            totalAmount: 19.99m,
            items: [(productId, "Test Product", 1, 19.99m, (Guid?)null, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        order.Complete();
        var key = OrderLicenseKey.Create(order.Id, order.Items.Single().Id, productId, Plaintext);
        db.Orders.Add(order);
        db.OrderLicenseKeys.Add(key);
        await db.SaveChangesAsync();
        return (db, order, key);
    }

    // ─── admin reveal (R19) ─────────────────────────────────────────────

    [Fact]
    public async Task AdminReveal_WithoutAReason_IsRefused_AndLeavesNoAuditRow()
    {
        var (db, order, key) = await SeedAsync();
        var handler = new RevealAdminOrderLicenseKeyQueryHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            new RevealAdminOrderLicenseKeyQuery(order.Id, key.Id, "  ", "203.0.113.9", "UA"), CancellationToken.None);

        Assert.Equal(CommerceErrors.RevealReasonRequired.Code, result.Error.Code);
        Assert.Empty(db.OrderAuditEntries);
    }

    [Fact]
    public async Task AdminReveal_RecordsReason_Ip_Device_AndTheKeyId_ButNotThePlaintext()
    {
        var (db, order, key) = await SeedAsync();
        var handler = new RevealAdminOrderLicenseKeyQueryHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            new RevealAdminOrderLicenseKeyQuery(order.Id, key.Id, "Customer reported the key as invalid", "203.0.113.9", "Mozilla/5.0"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Plaintext, result.Value.LicenseKey);

        var audit = Assert.Single(db.OrderAuditEntries);
        Assert.Equal("LicenseKeyRevealed", audit.EventType);
        Assert.Equal("admin-1", audit.ActorUserId);
        Assert.Equal("Customer reported the key as invalid", audit.Reason);
        Assert.Equal("203.0.113.9", audit.IpAddress);
        Assert.Equal("Mozilla/5.0", audit.UserAgent);
        Assert.Contains(key.Id.ToString(), audit.Description);
        Assert.DoesNotContain(Plaintext, audit.Description);
    }

    // ─── customer view logging (R20) ────────────────────────────────────

    private static RevealCustomerLibraryKeyQueryHandler CustomerHandler(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext db, string mode, FakeCustomerActionOtpService otp) =>
        new(
            db,
            new FakeCurrentUserService("user-1"),
            new FakeCommerceSettingsProvider { Security = new SecuritySettingsPayload(5, 15, false, CustomerRevealOtpMode: mode) },
            otp);

    [Fact]
    public async Task CustomerReveal_LogsOrder_User_Key_Time_Ip_AndDevice()
    {
        var (db, order, key) = await SeedAsync();
        var handler = CustomerHandler(db, "Off", new FakeCustomerActionOtpService());

        var result = await handler.Handle(
            new RevealCustomerLibraryKeyQuery(key.Id, null, "198.51.100.23", "Safari/17"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(db.OrderAuditEntries);
        Assert.Equal(order.Id, audit.OrderId);
        Assert.Equal("user-1", audit.ActorUserId);
        Assert.Equal("LicenseKeyRevealed", audit.EventType);
        Assert.Equal("198.51.100.23", audit.IpAddress);
        Assert.Equal("Safari/17", audit.UserAgent);
        Assert.Contains(key.Id.ToString(), audit.Description);
        Assert.True(audit.OccurredOnUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task CustomerReveal_AfterOtp_RecordsTheVerificationAsItsOwnAuditEvent()
    {
        var (db, order, key) = await SeedAsync();
        var handler = CustomerHandler(db, "FirstReveal", new FakeCustomerActionOtpService());

        var result = await handler.Handle(
            new RevealCustomerLibraryKeyQuery(key.Id, "123456", "198.51.100.23", "Safari/17"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, db.OrderAuditEntries.Count());
        var verified = Assert.Single(db.OrderAuditEntries.Where(e => e.EventType == "LicenseKeyRevealOtpVerified"));
        Assert.Equal(order.Id, verified.OrderId);
        Assert.Equal("198.51.100.23", verified.IpAddress);
        Assert.DoesNotContain("123456", verified.Description);
        Assert.Single(db.OrderAuditEntries.Where(e => e.EventType == "LicenseKeyRevealed"));
    }

    // ─── source-mode snapshot (R13) ─────────────────────────────────────

    [Theory]
    [InlineData(HAMBOX.Modules.Catalog.Domain.Enums.FulfillmentMode.ManualOnly, "ManualOnly")]
    [InlineData(HAMBOX.Modules.Catalog.Domain.Enums.FulfillmentMode.ManualFirst, "ManualFirst")]
    public async Task Fulfillment_SnapshotsTheSourceMode_OnTheOrderLine_AndKeepsTheFirstOne(
        HAMBOX.Modules.Catalog.Domain.Enums.FulfillmentMode mode, string expected)
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var order = Order.Create(
            userId: "user-1", orderNumber: $"ORD-{Guid.NewGuid():N}", email: "b@example.com", country: "US",
            paymentMethod: "development", subtotal: 10m, discountAmount: 0m, taxAmount: 0m, totalAmount: 10m,
            items: [(productId, "Test Product", 1, 10m, (Guid?)variantId, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync();

        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[variantId] = 1;
        inventory.CodesByVariant[variantId] = new Queue<string>(["KEY-0001"]);
        var router = new FakeFulfillmentRouter();
        router.SetReadiness(variantId, new(mode, true, null));

        var service = new OrderFulfillmentService(
            commerceDb, inventory, new NullSupplierFulfillmentService(), router, new NullSupplierPricingEngine(),
            HAMBOX.UnitTests.Suppliers.TestDoubles.SuppliersTestDbContextFactory.Create(),
            new FakeDeliveryTicketService(),
            NullLogger<OrderFulfillmentService>.Instance);

        await service.FulfillMissingAsync(order, CancellationToken.None);
        Assert.Equal(expected, order.Items.Single().FulfillmentModeSnapshot);

        // A later edit of the variant's mode must not rewrite how this order was sourced.
        order.Items.Single().RecordFulfillmentMode("SupplierOnly");
        Assert.Equal(expected, order.Items.Single().FulfillmentModeSnapshot);
    }
}
