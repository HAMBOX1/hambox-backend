using HAMBOX.Application.Communication;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.AssignTicket;

internal sealed class AssignTicketCommandHandler(
    ISupportDbContext dbContext, ICommunicationService communicationService, ISupportRealtimeNotifier realtimeNotifier)
    : IRequestHandler<AssignTicketCommand, Result>
{
    public async Task<Result> Handle(AssignTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        var previousAgent = ticket.AssignedAgentUserId;
        ticket.AssignTo(request.AgentUserId);

        dbContext.TicketAssignments.Add(
            TicketAssignment.Create(ticket.Id, previousAgent, request.AgentUserId, request.AssignedByUserId));
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(
            ticket.Id,
            previousAgent is null ? TicketAuditAction.Assigned : TicketAuditAction.Transferred,
            request.AssignedByUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        await communicationService.SendAsync(new CommunicationRequest(
            UserId: request.AgentUserId,
            TemplateKey: SupportTemplateKeys.TicketAssigned,
            Category: CommunicationCategory.Support,
            Variables: new Dictionary<string, string> { ["TicketNumber"] = ticket.TicketNumber, ["Subject"] = ticket.Subject },
            RelatedEntityType: "Ticket",
            RelatedEntityId: ticket.Id.ToString(),
            ActionUrl: $"/admin/support/tickets/{ticket.Id}"), cancellationToken);

        await realtimeNotifier.NotifyTicketUpdatedAsync(
            ticket.Id, ticket.CustomerUserId, ticket.AssignedAgentUserId, ticket.Status, cancellationToken);

        return Result.Success();
    }
}
