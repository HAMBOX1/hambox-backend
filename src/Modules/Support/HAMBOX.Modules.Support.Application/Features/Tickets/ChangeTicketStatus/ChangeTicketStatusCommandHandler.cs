using HAMBOX.Application.Communication;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ChangeTicketStatus;

internal sealed class ChangeTicketStatusCommandHandler(
    ISupportDbContext dbContext, ICommunicationService communicationService, ISupportRealtimeNotifier realtimeNotifier)
    : IRequestHandler<ChangeTicketStatusCommand, Result>
{
    public async Task<Result> Handle(ChangeTicketStatusCommand request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        var fromStatus = ticket.Status;
        if (fromStatus == request.NewStatus)
        {
            return Result.Success();
        }

        ticket.ChangeStatus(request.NewStatus);

        dbContext.TicketStatusHistories.Add(
            TicketStatusHistory.Create(ticket.Id, fromStatus, request.NewStatus, request.ChangedByUserId));
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(ticket.Id, TicketAuditAction.StatusChanged, request.ChangedByUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        var templateKey = request.NewStatus == TicketStatus.Resolved
            ? SupportTemplateKeys.TicketResolved
            : SupportTemplateKeys.TicketStatusChanged;

        await communicationService.SendAsync(new CommunicationRequest(
            UserId: ticket.CustomerUserId,
            TemplateKey: templateKey,
            Category: CommunicationCategory.Support,
            Variables: new Dictionary<string, string>
            {
                ["TicketNumber"] = ticket.TicketNumber,
                ["Status"] = request.NewStatus.ToString(),
            },
            RelatedEntityType: "Ticket",
            RelatedEntityId: ticket.Id.ToString(),
            ActionUrl: $"/account/support/tickets/{ticket.Id}"), cancellationToken);

        await realtimeNotifier.NotifyTicketUpdatedAsync(
            ticket.Id, ticket.CustomerUserId, ticket.AssignedAgentUserId, ticket.Status, cancellationToken);

        return Result.Success();
    }
}
