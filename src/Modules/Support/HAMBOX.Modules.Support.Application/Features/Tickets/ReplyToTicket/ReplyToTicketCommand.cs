using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ReplyToTicket;

public sealed record ReplyToTicketCommand(
    Guid TicketId,
    string AuthorUserId,
    TicketMessageAuthorRole AuthorRole,
    string Body,
    bool IsInternal,
    IReadOnlyList<Guid>? AttachmentIds,
    Guid? SavedReplyId) : IRequest<Result<TicketMessageDto>>;
