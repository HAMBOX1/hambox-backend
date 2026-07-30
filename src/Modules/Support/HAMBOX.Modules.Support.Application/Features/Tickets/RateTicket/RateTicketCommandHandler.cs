using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.RateTicket;

internal sealed class RateTicketCommandHandler(ISupportDbContext dbContext) : IRequestHandler<RateTicketCommand, Result>
{
    public async Task<Result> Handle(RateTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        if (ticket.CustomerUserId != request.CustomerUserId)
        {
            return Result.Failure(SupportErrors.NotYourTicket);
        }

        if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
        {
            return Result.Failure(SupportErrors.CannotRate);
        }

        if (ticket.RatingScore is not null)
        {
            return Result.Failure(SupportErrors.AlreadyRated);
        }

        ticket.Rate(request.Score, request.Comment);
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(ticket.Id, TicketAuditAction.Rated, request.CustomerUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
