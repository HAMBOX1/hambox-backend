using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ChangeTicketPriority;

internal sealed class ChangeTicketPriorityCommandHandler(ISupportDbContext dbContext, ISupportRealtimeNotifier realtimeNotifier)
    : IRequestHandler<ChangeTicketPriorityCommand, Result>
{
    public async Task<Result> Handle(ChangeTicketPriorityCommand request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        if (request.PriorityId is Guid priorityId)
        {
            var priorityExists = await dbContext.TicketPriorities.AnyAsync(p => p.Id == priorityId, cancellationToken);
            if (!priorityExists)
            {
                return Result.Failure(SupportErrors.PriorityNotFound);
            }
        }

        ticket.SetPriority(request.PriorityId);
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(ticket.Id, TicketAuditAction.PriorityChanged, request.ChangedByUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        await realtimeNotifier.NotifyTicketUpdatedAsync(
            ticket.Id, ticket.CustomerUserId, ticket.AssignedAgentUserId, ticket.Status, cancellationToken);

        return Result.Success();
    }
}
