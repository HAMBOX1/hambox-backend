using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Features.Tickets.GetTicketById;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.UnitTests.Support.TestDoubles;

namespace HAMBOX.UnitTests.Support;

/// <summary>
/// Covers ticket-detail ownership and message scoping — the two guarantees the AI Assistant's
/// ticket awareness depends on, since it reads this same handler's output through the real
/// customer-facing endpoint (no parallel/looser path was added for the assistant).
/// </summary>
public sealed class GetTicketByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_OwnerRequestsOwnTicket_ReturnsDetailWithMessages()
    {
        var (supportDb, identityDb, contextBuilder) = SupportTestDbContextFactory.Create();

        var ticket = Ticket.Create("TCK-1", "Can't redeem my key", "customer-1", null, null, null, null, null, null, null, null);
        supportDb.Tickets.Add(ticket);
        var customerMessage = TicketMessage.Create(ticket.Id, "customer-1", TicketMessageAuthorRole.Customer, "It says invalid code.", false);
        var agentMessage = TicketMessage.Create(ticket.Id, "agent-1", TicketMessageAuthorRole.Agent, "Please try again, we refreshed the code.", false);
        supportDb.TicketMessages.AddRange(customerMessage, agentMessage);
        await supportDb.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTicketByIdQueryHandler(supportDb, identityDb, contextBuilder);
        var result = await handler.Handle(new GetTicketByIdQuery(ticket.Id, "customer-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("TCK-1", result.Value.TicketNumber);
        Assert.Equal(2, result.Value.Messages.Count);
    }

    [Fact]
    public async Task Handle_DifferentCustomerRequestsTicket_ReturnsNotYourTicket()
    {
        var (supportDb, identityDb, contextBuilder) = SupportTestDbContextFactory.Create();

        var ticket = Ticket.Create("TCK-1", "Can't redeem my key", "customer-1", null, null, null, null, null, null, null, null);
        supportDb.Tickets.Add(ticket);
        await supportDb.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTicketByIdQueryHandler(supportDb, identityDb, contextBuilder);
        var result = await handler.Handle(new GetTicketByIdQuery(ticket.Id, "customer-2"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SupportErrors.NotYourTicket.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_UnknownTicketId_ReturnsTicketNotFound()
    {
        var (supportDb, identityDb, contextBuilder) = SupportTestDbContextFactory.Create();

        var handler = new GetTicketByIdQueryHandler(supportDb, identityDb, contextBuilder);
        var result = await handler.Handle(new GetTicketByIdQuery(Guid.NewGuid(), "customer-1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SupportErrors.TicketNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_CustomerRequest_ExcludesInternalAgentNotes()
    {
        var (supportDb, identityDb, contextBuilder) = SupportTestDbContextFactory.Create();

        var ticket = Ticket.Create("TCK-1", "Can't redeem my key", "customer-1", null, null, null, null, null, null, null, null);
        supportDb.Tickets.Add(ticket);
        var customerMessage = TicketMessage.Create(ticket.Id, "customer-1", TicketMessageAuthorRole.Customer, "It says invalid code.", false);
        var internalNote = TicketMessage.Create(ticket.Id, "agent-1", TicketMessageAuthorRole.Agent, "Escalate to supplier team.", true);
        supportDb.TicketMessages.AddRange(customerMessage, internalNote);
        await supportDb.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTicketByIdQueryHandler(supportDb, identityDb, contextBuilder);
        var result = await handler.Handle(new GetTicketByIdQuery(ticket.Id, "customer-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var message = Assert.Single(result.Value.Messages);
        Assert.False(message.IsInternal);
        Assert.DoesNotContain(result.Value.Messages, m => m.Body == "Escalate to supplier team.");
    }
}
