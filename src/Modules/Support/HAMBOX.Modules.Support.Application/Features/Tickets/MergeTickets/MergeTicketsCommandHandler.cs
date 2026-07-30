using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.MergeTickets;

internal sealed class MergeTicketsCommandHandler(ISupportDbContext dbContext) : IRequestHandler<MergeTicketsCommand, Result>
{
    public async Task<Result> Handle(MergeTicketsCommand request, CancellationToken cancellationToken)
    {
        if (request.SourceTicketId == request.TargetTicketId)
        {
            return Result.Failure(SupportErrors.CannotMergeIntoSelf);
        }

        var source = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.SourceTicketId, cancellationToken);
        if (source is null)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        var targetExists = await dbContext.Tickets.AnyAsync(t => t.Id == request.TargetTicketId, cancellationToken);
        if (!targetExists)
        {
            return Result.Failure(SupportErrors.TargetTicketNotFound);
        }

        source.MergeInto(request.TargetTicketId);
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(
            source.Id, TicketAuditAction.Merged, request.MergedByUserId, $"{{\"targetTicketId\":\"{request.TargetTicketId}\"}}"));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
