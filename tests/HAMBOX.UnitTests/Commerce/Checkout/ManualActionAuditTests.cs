using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.BulkAdminOrders;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.ManualDelivery;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.UpdateAdminOrderStatus;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// Audit R42 / R43 / R47: every manual order action needs a reason and leaves who / when / from where / old to new;
/// "mark as delivered" needs reason AND evidence; the customer can be contacted from the order; a notification
/// failure never reaches the code that asked for it.
/// </summary>
public sealed class ManualActionAuditTests
{
    private static Order PaidOrder(OrderStatus? status = null)
    {
        var order = Order.Create(
            "user-1", $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", "dot", 20m, 0m, 0m, 20m,
            [(Guid.NewGuid(), "Gift card", 1, 20m, (Guid?)null, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        order.RecordPayment("Dot", $"txn-{Guid.NewGuid():N}");
        if (status == OrderStatus.Processing)
        {
            order.MarkProcessing();
        }

        return order;
    }

    private sealed class NullSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            Task.FromResult(default(TResponse)!);

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => Task.CompletedTask;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static UpdateAdminOrderStatusCommandHandler StatusHandler(CommerceDbContext db, string adminId)
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var inventory = new FakeInventoryEngine(catalogDb);
        var communication = new FakeCommunicationService();
        var referral = new ReferralLifecycleService(
            db, new FakeDotPlatformSettingsProvider(), new ReferralRewardService(db, new FakeMembershipEngine()),
            communication, NullLogger<ReferralLifecycleService>.Instance);
        return new UpdateAdminOrderStatusCommandHandler(
            db, new FakeCommerceTransactionService(), new FakeCurrentUserService(adminId),
            new OrderInventoryReleaseService(catalogDb, inventory), referral, new NullSender());
    }

    // ─── reasons are mandatory ──────────────────────────────────────────

    [Fact]
    public async Task AStatusChange_WithoutAReason_IsRefused_AndNothingChanges()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var order = PaidOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await StatusHandler(db, "admin-1").Handle(
            new UpdateAdminOrderStatusCommand(order.Id, new UpdateAdminOrderStatusRequest("Cancelled", "  ")), CancellationToken.None);

        Assert.Equal(CommerceErrors.ActionReasonRequired.Code, result.Error.Code);
        Assert.NotEqual(OrderStatus.Cancelled, order.Status);
        Assert.Empty(db.OrderAuditEntries);
    }

    [Fact]
    public async Task AStatusChange_RecordsReason_Actor_OldAndNewStatus()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var order = PaidOrder(OrderStatus.Processing);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await StatusHandler(db, "admin-1").Handle(
            new UpdateAdminOrderStatusCommand(order.Id, new UpdateAdminOrderStatusRequest("Cancelled", "Customer asked to cancel")),
            CancellationToken.None);

        var entry = Assert.Single(db.OrderAuditEntries, e => e.EventType == "StatusChanged");
        Assert.Equal("Customer asked to cancel", entry.Reason);
        Assert.Equal("Processing", entry.OldValue);
        Assert.Equal("Cancelled", entry.NewValue);
        Assert.Equal("admin-1", entry.ActorUserId);
        Assert.True(entry.OccurredOnUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void AuditRows_TakeTheRequestOrigin_ButNeverOverwriteOneThatWasGiven()
    {
        var entry = OrderAuditEntry.Create(Guid.NewGuid(), "NoteAdded", "A note");
        Assert.Null(entry.IpAddress);

        entry.StampOrigin("198.51.100.7", "Safari/17");
        Assert.Equal("198.51.100.7", entry.IpAddress);
        Assert.Equal("Safari/17", entry.UserAgent);

        entry.StampOrigin("10.0.0.1", "Other");
        Assert.Equal("198.51.100.7", entry.IpAddress);
    }

    // ─── mark as delivered ──────────────────────────────────────────────

    [Fact]
    public async Task MarkDelivered_NeedsReasonAndEvidence()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var order = PaidOrder(OrderStatus.Processing);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var handler = new MarkOrderDeliveredManuallyCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var noReason = await handler.Handle(new MarkOrderDeliveredManuallyCommand(order.Id, "", "chat #44"), CancellationToken.None);
        Assert.Equal(CommerceErrors.ActionReasonRequired.Code, noReason.Error.Code);

        var noEvidence = await handler.Handle(new MarkOrderDeliveredManuallyCommand(order.Id, "Delivered by chat", " "), CancellationToken.None);
        Assert.Equal(CommerceErrors.DeliveryEvidenceRequired.Code, noEvidence.Error.Code);

        Assert.NotEqual(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public async Task MarkDelivered_CompletesThePaidOrder_RecordsEvidence_AndLiftsTheHold()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var order = PaidOrder(OrderStatus.Processing);
        order.PlaceOnHold(OrderHoldKind.ManualReview, "Sensitive product");
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await new MarkOrderDeliveredManuallyCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin"))
            .Handle(new MarkOrderDeliveredManuallyCommand(order.Id, "Delivered over support chat", "Ticket #5521"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.False(order.IsOnHold);
        var entry = Assert.Single(db.OrderAuditEntries, e => e.EventType == "ManuallyMarkedDelivered");
        Assert.Contains("Ticket #5521", entry.Description);
        Assert.Equal("Delivered over support chat", entry.Reason);
        Assert.Equal("Processing", entry.OldValue);
        Assert.Equal("Completed", entry.NewValue);
    }

    [Fact]
    public async Task MarkDelivered_RefusesAnUnpaidOrder()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var order = Order.Create(
            "user-1", $"ORD-{Guid.NewGuid():N}", "b@example.com", "US", "dot", 5m, 0m, 0m, 5m,
            [(Guid.NewGuid(), "Card", 1, 5m, (Guid?)null, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var result = await new MarkOrderDeliveredManuallyCommandHandler(db, new FakeCurrentUserService("admin-1"))
            .Handle(new MarkOrderDeliveredManuallyCommand(order.Id, "Delivered by chat", "chat #1"), CancellationToken.None);

        Assert.Equal(CommerceErrors.OrderNotPaidForDelivery.Code, result.Error.Code);
    }

    // ─── contact the customer ───────────────────────────────────────────

    [Fact]
    public async Task ContactCustomer_OpensATicketForTheOrder_AndAuditsIt()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var order = PaidOrder();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var tickets = new FakeDeliveryTicketService();

        var result = await new ContactOrderCustomerCommandHandler(db, new FakeCurrentUserService("admin-1"), tickets)
            .Handle(new ContactOrderCustomerCommand(order.Id, "About your order", "Could you confirm your account name?"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var ticket = Assert.Single(tickets.Requests);
        Assert.Equal("user-1", ticket.CustomerUserId);
        Assert.Equal(order.Id, ticket.RelatedOrderId);
        Assert.Contains(db.OrderAuditEntries, e => e.EventType == "CustomerContacted");
    }

    // ─── bulk ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkActions_NeedAReason()
    {
        var handler = new BulkAdminOrdersCommandHandler(new NullSender());

        var noReason = await handler.Handle(
            new BulkAdminOrdersCommand(new BulkAdminOrdersRequest("cancel", [Guid.NewGuid()], null)), CancellationToken.None);

        Assert.Equal(CommerceErrors.ActionReasonRequired.Code, noReason.Error.Code);
    }

    // ─── R47: a notification failure is invisible to the caller ─────────

    private sealed class ExplodingCommunication : HAMBOX.Application.Communication.ICommunicationService
    {
        public Task<Result> SendAsync(
            HAMBOX.Application.Communication.CommunicationRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Template store unavailable");
    }

    [Fact]
    public async Task ANotificationThatThrows_BecomesAFailedResult_NeverAnException()
    {
        var failSoft = new HAMBOX.Modules.Communication.Infrastructure.Services.FailSoftCommunicationService(
            new ExplodingCommunication(),
            NullLogger<HAMBOX.Modules.Communication.Infrastructure.Services.FailSoftCommunicationService>.Instance);

        var result = await failSoft.SendAsync(new HAMBOX.Application.Communication.CommunicationRequest(
            UserId: "user-1",
            TemplateKey: "OrderConfirmation",
            Category: HAMBOX.Application.Communication.CommunicationCategory.Order,
            Variables: new Dictionary<string, string>()));

        Assert.True(result.IsFailure);
    }
}
