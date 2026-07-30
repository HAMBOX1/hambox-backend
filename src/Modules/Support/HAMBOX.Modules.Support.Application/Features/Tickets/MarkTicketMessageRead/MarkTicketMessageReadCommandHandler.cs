using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.MarkTicketMessageRead;

internal sealed class MarkTicketMessageReadCommandHandler(ISupportDbContext dbContext, ISupportRealtimeNotifier realtimeNotifier)
    : IRequestHandler<MarkTicketMessageReadCommand, Result>
{
    public async Task<Result> Handle(MarkTicketMessageReadCommand request, CancellationToken cancellationToken)
    {
        var message = await dbContext.TicketMessages
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.TicketId == request.TicketId, cancellationToken);

        if (message is null)
        {
            return Result.Failure(SupportErrors.TicketNotFound);
        }

        message.MarkDelivered();
        message.MarkRead();
        await dbContext.SaveChangesAsync(cancellationToken);

        await realtimeNotifier.NotifyMessageStatusAsync(
            request.TicketId, request.MessageId, isDelivered: true, isRead: true, cancellationToken);

        return Result.Success();
    }
}
