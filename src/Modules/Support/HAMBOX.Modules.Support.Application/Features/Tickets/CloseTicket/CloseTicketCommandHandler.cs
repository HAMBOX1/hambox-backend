using HAMBOX.Application.Communication;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.CloseTicket;

internal sealed class CloseTicketCommandHandler(
    ISupportDbContext dbContext, ICommunicationService communicationService, ISupportRealtimeNotifier realtimeNotifier)
    : IRequestHandler<CloseTicketCommand, Result>
{
    public async Task<Result> Handle(CloseTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        if (request.IsCustomerRequest && ticket.CustomerUserId != request.RequestedByUserId)
        {
            return Result.Failure(SupportErrors.NotYourTicket);
        }

        var fromStatus = ticket.Status;
        ticket.ChangeStatus(TicketStatus.Closed);

        dbContext.TicketStatusHistories.Add(
            TicketStatusHistory.Create(ticket.Id, fromStatus, TicketStatus.Closed, request.RequestedByUserId));
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(ticket.Id, TicketAuditAction.StatusChanged, request.RequestedByUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        var notifyUserId = request.IsCustomerRequest ? ticket.AssignedAgentUserId : ticket.CustomerUserId;
        if (!string.IsNullOrWhiteSpace(notifyUserId))
        {
            await communicationService.SendAsync(new CommunicationRequest(
                UserId: notifyUserId,
                TemplateKey: SupportTemplateKeys.TicketClosed,
                Category: CommunicationCategory.Support,
                Variables: new Dictionary<string, string> { ["TicketNumber"] = ticket.TicketNumber },
                RelatedEntityType: "Ticket",
                RelatedEntityId: ticket.Id.ToString()), cancellationToken);
        }

        await realtimeNotifier.NotifyTicketUpdatedAsync(
            ticket.Id, ticket.CustomerUserId, ticket.AssignedAgentUserId, ticket.Status, cancellationToken);

        return Result.Success();
    }
}
