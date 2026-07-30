using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.DeleteTicket;

internal sealed class DeleteTicketCommandHandler(ISupportDbContext dbContext) : IRequestHandler<DeleteTicketCommand, Result>
{
    public async Task<Result> Handle(DeleteTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        ticket.Delete();
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(ticket.Id, TicketAuditAction.Deleted, request.DeletedByUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
