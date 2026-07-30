using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tags.RemoveTagFromTicket;

internal sealed class RemoveTagFromTicketCommandHandler(ISupportDbContext dbContext) : IRequestHandler<RemoveTagFromTicketCommand, Result>
{
    public async Task<Result> Handle(RemoveTagFromTicketCommand request, CancellationToken cancellationToken)
    {
        var assignment = await dbContext.TicketTagAssignments
            .FirstOrDefaultAsync(a => a.TicketId == request.TicketId && a.TagId == request.TagId, cancellationToken);

        if (assignment is null)
        {
            return Result.Success();
        }

        dbContext.TicketTagAssignments.Remove(assignment);
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(request.TicketId, TicketAuditAction.TagRemoved, request.ActorUserId));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
