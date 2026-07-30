using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tags.AddTagToTicket;

internal sealed class AddTagToTicketCommandHandler(ISupportDbContext dbContext) : IRequestHandler<AddTagToTicketCommand, Result>
{
    public async Task<Result> Handle(AddTagToTicketCommand request, CancellationToken cancellationToken)
    {
        var ticketExists = await dbContext.Tickets.AnyAsync(t => t.Id == request.TicketId, cancellationToken);
        if (!ticketExists)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        var tagExists = await dbContext.TicketTags.AnyAsync(t => t.Id == request.TagId, cancellationToken);
        if (!tagExists)
        {
            return Result.Failure(SupportErrors.TagNotFound);
        }

        var alreadyAssigned = await dbContext.TicketTagAssignments
            .AnyAsync(a => a.TicketId == request.TicketId && a.TagId == request.TagId, cancellationToken);
        if (alreadyAssigned)
        {
            return Result.Success();
        }

        dbContext.TicketTagAssignments.Add(TicketTagAssignment.Create(request.TicketId, request.TagId));
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(request.TicketId, TicketAuditAction.TagAdded, request.ActorUserId));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
