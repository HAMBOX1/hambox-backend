using HAMBOX.Modules.Commerce.Application.Features.Account.Library;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Library;

/// <summary>
/// Proves a successful customer license-key reveal now writes an <see cref="OrderAuditEntry"/> —
/// mirroring the admin reveal path's existing audit trail — while the ownership/authorization check
/// itself (already correct, no IDOR) is unchanged, and the plaintext code is never written into the
/// audit description.
/// </summary>
public sealed class RevealCustomerLibraryKeyAuditTests
{
    private const string PlaintextLicenseKey = "REAL-SECRET-CODE-XYZ-0001";

    private static (Order Order, OrderLicenseKey Key) SeedOrderWithLicenseKey(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext db, string userId)
    {
        var productId = Guid.NewGuid();
        var order = Order.Create(
            userId: userId,
            orderNumber: $"ORD-{Guid.NewGuid():N}",
            email: "buyer@example.com",
            country: "US",
            paymentMethod: "development",
            subtotal: 19.99m,
            discountAmount: 0m,
            taxAmount: 0m,
            totalAmount: 19.99m,
            items: [(productId, "Test Product", 1, 19.99m, null, null)]);

        order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        order.Complete();
        var orderItemId = order.Items.Single().Id;

        var key = OrderLicenseKey.Create(order.Id, orderItemId, productId, PlaintextLicenseKey);

        db.Orders.Add(order);
        db.OrderLicenseKeys.Add(key);
        return (order, key);
    }

    [Fact]
    public async Task Handle_SuccessfulReveal_WritesOrderAuditEntry_WithoutThePlaintextKey()
    {
        var (commerceDb, _) = CommerceTestDbContextFactory.Create();
        var (order, key) = SeedOrderWithLicenseKey(commerceDb, "user-1");
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var handler = new RevealCustomerLibraryKeyQueryHandler(commerceDb, new FakeCurrentUserService("user-1"));
        var result = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlaintextLicenseKey, result.Value.LicenseKey);

        var auditEntry = Assert.Single(commerceDb.OrderAuditEntries.Where(e => e.OrderId == order.Id));
        Assert.Equal("LicenseKeyRevealed", auditEntry.EventType);
        Assert.Equal("user-1", auditEntry.ActorUserId);
        Assert.DoesNotContain(PlaintextLicenseKey, auditEntry.Description, StringComparison.Ordinal);
        Assert.Contains(key.Id.ToString(), auditEntry.Description, StringComparison.Ordinal);
    }

    // Ownership check is unchanged by this fix — proven here so a regression in the audit-entry
    // addition (e.g. writing it before the ownership check) would be caught immediately.
    [Fact]
    public async Task Handle_AnotherUsersLibraryItem_FailsAndWritesNoAuditEntry()
    {
        var (commerceDb, _) = CommerceTestDbContextFactory.Create();
        var (_, key) = SeedOrderWithLicenseKey(commerceDb, "owning-user");
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var handler = new RevealCustomerLibraryKeyQueryHandler(commerceDb, new FakeCurrentUserService("someone-else"));
        var result = await handler.Handle(new RevealCustomerLibraryKeyQuery(key.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(commerceDb.OrderAuditEntries);
    }
}
