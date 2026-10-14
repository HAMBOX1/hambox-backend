using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.SettleOrderRefund;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// Audit R33: refunding or cancelling an order that was paid through a real gateway leaves a visible "money still
/// owed" marker until an admin records the gateway reference of the refund they made.
/// </summary>
public sealed class RefundSettlementTests
{
    private static Order PaidOrder(string provider, decimal total = 25m)
    {
        var order = Order.Create(
            "user-1", $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", provider.ToLowerInvariant(),
            total, 0m, 0m, total,
            [(Guid.NewGuid(), "Gift card", 1, total, (Guid?)null, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        order.RecordPayment(provider, $"txn-{Guid.NewGuid():N}");
        return order;
    }

    [Theory]
    [InlineData("Dot")]
    [InlineData("DotFawry")]
    [InlineData("Cryptomus")]
    [InlineData("OxaPay")]
    public void RefundingAGatewayPaidOrder_LeavesTheRefundOwed(string provider)
    {
        var order = PaidOrder(provider);

        order.Refund();

        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(RefundSettlementStatus.AwaitingGatewayRefund, order.RefundSettlement);
    }

    [Fact]
    public void ADevelopmentPayment_HasNoMoneyToReturn()
    {
        var order = PaidOrder("Development");

        order.Refund();

        Assert.Equal(RefundSettlementStatus.None, order.RefundSettlement);
    }

    [Fact]
    public void CancellingAPaidGatewayOrder_AlsoFlagsTheRefund_ButCancellingAnUnpaidOneDoesNot()
    {
        var paid = PaidOrder("Cryptomus");
        paid.SetAdminStatus(OrderStatus.Cancelled);
        Assert.Equal(RefundSettlementStatus.AwaitingGatewayRefund, paid.RefundSettlement);

        var unpaid = Order.Create(
            "user-1", $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", "cryptomus", 10m, 0m, 0m, 10m,
            [(Guid.NewGuid(), "Card", 1, 10m, (Guid?)null, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        unpaid.SetAdminStatus(OrderStatus.Cancelled);
        Assert.Equal(RefundSettlementStatus.None, unpaid.RefundSettlement);
    }

    [Fact]
    public async Task SettlingRequiresAReference_AndRecordsWhoAndWhen()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var order = PaidOrder("Dot");
        order.Refund();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var handler = new SettleOrderRefundCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin One"));

        var missing = await handler.Handle(new SettleOrderRefundCommand(order.Id, "  ", null), CancellationToken.None);
        Assert.Equal(CommerceErrors.RefundReferenceRequired.Code, missing.Error.Code);
        Assert.Equal(RefundSettlementStatus.AwaitingGatewayRefund, order.RefundSettlement);

        var ok = await handler.Handle(
            new SettleOrderRefundCommand(order.Id, "DOT-REFUND-991", "Returned to the wallet"), CancellationToken.None);

        Assert.True(ok.IsSuccess);
        Assert.Equal(RefundSettlementStatus.Settled, order.RefundSettlement);
        Assert.Equal("DOT-REFUND-991", order.RefundGatewayReference);
        Assert.Equal("admin-1", order.RefundSettledByUserId);
        Assert.NotNull(order.RefundSettledOnUtc);
        Assert.Contains(db.OrderAuditEntries, e => e.EventType == "RefundSettled" && e.Description.Contains("DOT-REFUND-991"));
    }

    [Fact]
    public async Task AnOrderWithNothingOwed_CannotBeSettled()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var order = PaidOrder("Dot");
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await new SettleOrderRefundCommandHandler(db, new FakeCurrentUserService("admin-1"))
            .Handle(new SettleOrderRefundCommand(order.Id, "REF-1", null), CancellationToken.None);

        Assert.Equal(CommerceErrors.OrderHasNoRefundToSettle.Code, result.Error.Code);
    }
}
