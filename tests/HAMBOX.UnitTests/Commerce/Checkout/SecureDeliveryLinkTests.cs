using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.ResendAdminOrderCodes;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>Audit R48: delivery e-mails carry a link to the owner-only library, never the code itself.</summary>
public sealed class SecureDeliveryLinkTests
{
    [Fact]
    public async Task ResendingCodes_EmailsALibraryLink_AndNeverTheCode()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var order = Order.Create(
            "user-1", $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", "dot", 20m, 0m, 0m, 20m,
            [(Guid.NewGuid(), "Gift card", 1, 20m, (Guid?)null, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        db.Orders.Add(order);
        db.OrderLicenseKeys.Add(OrderLicenseKey.Create(order.Id, order.Items.First().Id, Guid.NewGuid(), "SECRET-KEY-1234", null));
        await db.SaveChangesAsync();
        var communication = new FakeCommunicationService();

        var result = await new ResendAdminOrderCodesCommandHandler(db, new FakeCurrentUserService("admin-1"), communication)
            .Handle(new ResendAdminOrderCodesCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(communication.SentRequests);
        Assert.All(sent.Variables!.Values, v => Assert.DoesNotContain("SECRET-KEY-1234", v));
        Assert.StartsWith("/account/library", sent.ActionUrl);
    }
}
