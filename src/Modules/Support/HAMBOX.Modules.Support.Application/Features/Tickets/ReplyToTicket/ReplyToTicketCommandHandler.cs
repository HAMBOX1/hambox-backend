using HAMBOX.Application.Communication;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ReplyToTicket;

internal sealed class ReplyToTicketCommandHandler(
    ISupportDbContext dbContext, IIdentityDbContext identityDb, ICommunicationService communicationService,
    ISupportRealtimeNotifier realtimeNotifier)
    : IRequestHandler<ReplyToTicketCommand, Result<TicketMessageDto>>
{
    public async Task<Result<TicketMessageDto>> Handle(ReplyToTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketMessageDto>(SupportErrors.TicketNotFound);
        }

        var message = TicketMessage.Create(
            ticket.Id, request.AuthorUserId, request.AuthorRole, request.Body, request.IsInternal, request.SavedReplyId);
        dbContext.TicketMessages.Add(message);

        if (request.AttachmentIds is { Count: > 0 })
        {
            var attachments = await dbContext.TicketAttachments
                .Where(a => request.AttachmentIds.Contains(a.Id) && a.TicketId == ticket.Id)
                .ToListAsync(cancellationToken);
            foreach (var attachment in attachments)
            {
                attachment.AttachToMessage(message.Id);
            }
        }

        var now = DateTimeOffset.UtcNow;
        if (request.AuthorRole == TicketMessageAuthorRole.Customer)
        {
            ticket.RecordCustomerMessage(now);
        }
        else if (request.AuthorRole == TicketMessageAuthorRole.Agent)
        {
            ticket.RecordAgentMessage(now, request.IsInternal);
        }

        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(
            ticket.Id, request.IsInternal ? TicketAuditAction.InternalNoteAdded : TicketAuditAction.Replied, request.AuthorUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        var author = await UserDisplayResolver.ResolveOneAsync(identityDb, request.AuthorUserId, cancellationToken);
        var attachmentDtos = await dbContext.TicketAttachments
            .Where(a => a.MessageId == message.Id)
            .Select(a => a)
            .ToListAsync(cancellationToken);

        if (!request.IsInternal)
        {
            var notifyUserId = request.AuthorRole == TicketMessageAuthorRole.Customer
                ? ticket.AssignedAgentUserId
                : ticket.CustomerUserId;

            if (!string.IsNullOrWhiteSpace(notifyUserId))
            {
                await communicationService.SendAsync(new CommunicationRequest(
                    UserId: notifyUserId,
                    TemplateKey: SupportTemplateKeys.NewReply,
                    Category: CommunicationCategory.Support,
                    Variables: new Dictionary<string, string> { ["TicketNumber"] = ticket.TicketNumber },
                    RelatedEntityType: "Ticket",
                    RelatedEntityId: ticket.Id.ToString(),
                    ActionUrl: request.AuthorRole == TicketMessageAuthorRole.Customer
                        ? $"/admin/support/tickets/{ticket.Id}"
                        : $"/account/support/tickets/{ticket.Id}"), cancellationToken);
            }
        }

        var messageDto = SupportMapper.ToDto(message, author?.Name ?? "Unknown", attachmentDtos.Select(SupportMapper.ToDto).ToList());
        await realtimeNotifier.NotifyNewMessageAsync(ticket.Id, messageDto, cancellationToken);

        return Result.Success(messageDto);
    }
}
