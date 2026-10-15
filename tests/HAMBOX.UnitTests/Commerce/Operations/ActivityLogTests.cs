using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Operations.ActivityLog;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Operations;

/// <summary>Audit R51 / R56: one searchable activity log with the full field set, per-entity export, click logging.</summary>
public sealed class ActivityLogTests
{
    private static GetActivityLogQuery Query(
        string? search = null, string? source = null, string? entityId = null, int pageSize = 50) =>
        new(search, source, entityId, null, null, 1, pageSize);

    [Fact]
    public async Task TheLog_CombinesTheAuditTrails_NewestFirst_WithTheFullFieldSet()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var orderId = Guid.NewGuid();
        db.OrderAuditEntries.Add(OrderAuditEntry.Create(
            orderId, "StatusChanged", "Status changed", "admin-1", "admin@example.com",
            reason: "Customer asked", ipAddress: "198.51.100.7", userAgent: "Safari", oldValue: "Pending", newValue: "Cancelled"));
        db.OperationalAuditLogs.Add(OperationalAuditLog.Create("SettingsChanged", "admin-1", "admin@example.com", "Settings", "currency"));
        await db.SaveChangesAsync();

        var result = await new GetActivityLogQueryHandler(db).Handle(Query(), CancellationToken.None);

        Assert.Equal(2, result.Value.TotalCount);
        var order = Assert.Single(result.Value.Items, e => e.Source == "Order");
        Assert.Equal("Pending", order.OldValue);
        Assert.Equal("Cancelled", order.NewValue);
        Assert.Equal("198.51.100.7", order.IpAddress);
        Assert.Equal("Customer asked", order.Reason);
        Assert.Equal(orderId.ToString(), order.EntityId);
    }

    [Fact]
    public async Task Search_FindsByEventReasonOrActor_AndEntityFilterNarrowsToOneOrder()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        db.OrderAuditEntries.Add(OrderAuditEntry.Create(first, "RefundIssued", "Refunded", "a", "alice@example.com", reason: "Duplicate charge"));
        db.OrderAuditEntries.Add(OrderAuditEntry.Create(second, "NoteAdded", "A note", "b", "bob@example.com"));
        await db.SaveChangesAsync();
        var handler = new GetActivityLogQueryHandler(db);

        var byReason = await handler.Handle(Query(search: "Duplicate"), CancellationToken.None);
        var byEntity = await handler.Handle(Query(entityId: second.ToString()), CancellationToken.None);

        Assert.Equal("RefundIssued", Assert.Single(byReason.Value.Items).EventType);
        Assert.Equal("NoteAdded", Assert.Single(byEntity.Value.Items).EventType);
    }

    [Fact]
    public void Csv_HasEveryColumn_AndNeutralisesSpreadsheetFormulas()
    {
        var csv = ActivityLogCsv.Build(
        [
            new ActivityLogEntryDto(
                Guid.NewGuid(), "Order", "StatusChanged", "=HYPERLINK(\"http://evil\")", "a", "alice, admin", "Order",
                "o-1", "needed", "Pending", "Cancelled", "198.51.100.7", "Safari",
                new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)),
        ]);

        var lines = csv.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Time (UTC),Source,Event,Description,Actor,Actor id,Entity,Entity id,Reason,Old value,New value,IP address,User agent", lines[0]);
        Assert.Contains("2026-10-01 12:00:00", lines[1]);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\")\"", lines[1]);
        Assert.Contains("\"alice, admin\"", lines[1]);
    }

    [Fact]
    public async Task AWhatsAppClick_IsLogged_WithTheOrderItCameFrom()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var handler = new LogSupportContactClickCommandHandler(db, new FakeCurrentUserService("user-1", "buyer@example.com"));

        var ok = await handler.Handle(
            new LogSupportContactClickCommand("whatsapp", "order-detail", "order-9", null, "/account/orders/order-9"),
            CancellationToken.None);
        var bad = await handler.Handle(
            new LogSupportContactClickCommand("carrier-pigeon", null, null, null, null), CancellationToken.None);

        Assert.True(ok.IsSuccess);
        var row = await db.OperationalAuditLogs.SingleAsync();
        Assert.Equal("SupportWhatsAppClicked", row.Action);
        Assert.Equal("Order", row.EntityType);
        Assert.Equal("order-9", row.EntityId);
        Assert.Equal("user-1", row.ActorUserId);
        Assert.Contains("context=order-detail", row.Details);
        Assert.Equal(CommerceErrors.SupportChannelInvalid.Code, bad.Error.Code);
    }
}
