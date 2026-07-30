using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.RenderSavedReply;

internal sealed class RenderSavedReplyQueryHandler(ISupportDbContext dbContext, TicketContextBuilder contextBuilder)
    : IRequestHandler<RenderSavedReplyQuery, Result<string>>
{
    public async Task<Result<string>> Handle(RenderSavedReplyQuery request, CancellationToken cancellationToken)
    {
        var reply = await dbContext.SavedReplies.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReplyId, cancellationToken);
        if (reply is null)
        {
            return Result.Failure<string>(SupportErrors.SavedReplyNotFound);
        }

        var ticket = await dbContext.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<string>(SupportErrors.TicketNotFound);
        }

        var context = await contextBuilder.BuildAsync(ticket, cancellationToken);
        var variables = new Dictionary<string, string>
        {
            ["CustomerName"] = context.CustomerName,
            ["OrderNumber"] = context.RelatedOrderNumber ?? context.RecentOrders.FirstOrDefault()?.OrderNumber ?? string.Empty,
            ["Product"] = context.RelatedProductName ?? context.RecentOrders.FirstOrDefault()?.ProductNames.FirstOrDefault() ?? string.Empty,
        };

        var rendered = SavedReplyRenderer.Render(reply.Body, variables);

        var replyEntity = await dbContext.SavedReplies.FirstOrDefaultAsync(r => r.Id == request.ReplyId, cancellationToken);
        replyEntity?.RecordUsage();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(rendered);
    }
}
