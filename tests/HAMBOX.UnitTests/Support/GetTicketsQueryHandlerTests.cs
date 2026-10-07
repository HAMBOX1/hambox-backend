using HAMBOX.Modules.Support.Application.Features.Tickets.GetTickets;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.UnitTests.Support.TestDoubles;

namespace HAMBOX.UnitTests.Support;

/// <summary>
/// Covers the scoping the AI Assistant's ticket-awareness relies on: the customer endpoint passes
/// <c>RequestingCustomerUserId</c> from the JWT, and the handler must never return another
/// customer's tickets no matter what else is asked for.
/// </summary>
public sealed class GetTicketsQueryHandlerTests
{
    [Fact]
    public async Task Handle_CustomerWithTickets_ReturnsOnlyThatCustomersTickets()
    {
        var (supportDb, identityDb, _) = SupportTestDbContextFactory.Create();

        var ownTicket1 = Ticket.Create("TCK-1", "Can't redeem my key", "customer-1", null, null, null, null, null, null, null, null);
        var ownTicket2 = Ticket.Create("TCK-2", "Where is my order", "customer-1", null, null, null, null, null, null, null, null);
        var otherCustomerTicket = Ticket.Create("TCK-3", "Billing question", "customer-2", null, null, null, null, null, null, null, null);
        supportDb.Tickets.AddRange(ownTicket1, ownTicket2, otherCustomerTicket);
        await supportDb.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTicketsQueryHandler(supportDb, identityDb);
        var result = await handler.Handle(
            new GetTicketsQuery("customer-1", 1, 20, null, null, null, null, null, null, null, "createdOnUtc", true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.All(result.Value.Items, t => Assert.Equal("customer-1", t.CustomerUserId));
        Assert.DoesNotContain(result.Value.Items, t => t.TicketNumber == "TCK-3");
    }

    [Fact]
    public async Task Handle_CustomerWithNoTickets_ReturnsEmptyResult_NotAnError()
    {
        var (supportDb, identityDb, _) = SupportTestDbContextFactory.Create();

        var otherCustomerTicket = Ticket.Create("TCK-9", "Unrelated", "customer-2", null, null, null, null, null, null, null, null);
        supportDb.Tickets.Add(otherCustomerTicket);
        await supportDb.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTicketsQueryHandler(supportDb, identityDb);
        var result = await handler.Handle(
            new GetTicketsQuery("customer-1", 1, 20, null, null, null, null, null, null, null, "createdOnUtc", true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }
}
