using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.UploadTicketAttachment;

internal sealed class UploadTicketAttachmentCommandHandler(
    ISupportDbContext dbContext, IFileStorage fileStorage, IAttachmentScanner attachmentScanner)
    : IRequestHandler<UploadTicketAttachmentCommand, Result<TicketAttachmentDto>>
{
    public async Task<Result<TicketAttachmentDto>> Handle(UploadTicketAttachmentCommand request, CancellationToken cancellationToken)
    {
        var ticketExists = await dbContext.Tickets.AnyAsync(t => t.Id == request.TicketId, cancellationToken);
        if (!ticketExists)
        {
            return Result.Failure<TicketAttachmentDto>(SupportErrors.TicketNotFound);
        }

        if (request.FileSizeBytes > fileStorage.MaxFileSizeBytes)
        {
            return Result.Failure<TicketAttachmentDto>(SupportErrors.AttachmentTooLarge);
        }

        if (!fileStorage.IsAllowedContentType(request.ContentType))
        {
            return Result.Failure<TicketAttachmentDto>(SupportErrors.AttachmentTypeNotAllowed);
        }

        var scanStatus = await attachmentScanner.ScanAsync(request.Content, request.FileName, cancellationToken);
        if (scanStatus == AttachmentScanStatus.Infected)
        {
            return Result.Failure<TicketAttachmentDto>(SupportErrors.AttachmentInfected);
        }

        if (request.Content.CanSeek)
        {
            request.Content.Seek(0, SeekOrigin.Begin);
        }

        var stored = await fileStorage.SaveAsync(
            request.Content, request.FileName, request.ContentType, $"support/tickets/{request.TicketId}", cancellationToken);

        var attachment = TicketAttachment.Create(
            request.TicketId, null, stored.StorageKey, stored.PublicUrl, stored.FileName, stored.ContentType,
            stored.FileSizeBytes, request.UploadedByUserId);
        attachment.SetScanStatus(scanStatus);

        dbContext.TicketAttachments.Add(attachment);
        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(request.TicketId, TicketAuditAction.AttachmentAdded, request.UploadedByUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(SupportMapper.ToDto(attachment));
    }
}
