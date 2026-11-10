using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.UploadTicketAttachment;

public sealed record UploadTicketAttachmentCommand(
    Guid TicketId,
    string UploadedByUserId,
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    // True for the customer endpoint: the ticket must belong to the uploader. Agents are authorized by permission instead.
    bool RequireTicketOwnership = false) : IRequest<Result<TicketAttachmentDto>>;
