using HAMBOX.Application.Communication;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ReopenTicket;

internal sealed class ReopenTicketCommandHandler(
    ISupportDbContext dbContext, ICommunicationService communicationService, ISupportRealtimeNotifier realtimeNotifier)
    : IRequestHandler<ReopenTicketCommand, Result>
{
    // Customers may reopen a resolved ticket within 7 days of resolution; closed tickets or
    // older resolutions require an agent (who bypasses this window entirely).
    private static readonly TimeSpan CustomerReopenWindow = TimeSpan.FromDays(7);

    public async Task<Result> Handle(ReopenTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        if (request.IsCustomerRequest)
        {
            if (ticket.CustomerUserId != request.RequestedByUserId)
            {
                return Result.Failure(SupportErrors.NotYourTicket);
            }

            var eligible = ticket.Status == TicketStatus.Resolved
                && ticket.ResolvedOnUtc is not null
                && DateTimeOffset.UtcNow - ticket.ResolvedOnUtc < CustomerReopenWindow;

            if (!eligible)
            {
                return Result.Failure(SupportErrors.CannotReopen);
            }
        }

        var fromStatus = ticket.Status;
        ticket.Reopen();

        dbContext.TicketStatusHistories.Add(
            TicketStatusHistory.Create(ticket.Id, fromStatus, ticket.Status, request.RequestedByUserId));
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(ticket.Id, TicketAuditAction.Reopened, request.RequestedByUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        var notifyUserId = request.IsCustomerRequest ? ticket.AssignedAgentUserId : ticket.CustomerUserId;
        if (!string.IsNullOrWhiteSpace(notifyUserId))
        {
            await communicationService.SendAsync(new CommunicationRequest(
                UserId: notifyUserId,
                TemplateKey: SupportTemplateKeys.TicketStatusChanged,
                Category: CommunicationCategory.Support,
                Variables: new Dictionary<string, string> { ["TicketNumber"] = ticket.TicketNumber, ["Status"] = "Reopened" },
                RelatedEntityType: "Ticket",
                RelatedEntityId: ticket.Id.ToString()), cancellationToken);
        }

        await realtimeNotifier.NotifyTicketUpdatedAsync(
            ticket.Id, ticket.CustomerUserId, ticket.AssignedAgentUserId, ticket.Status, cancellationToken);

        return Result.Success();
    }
}
