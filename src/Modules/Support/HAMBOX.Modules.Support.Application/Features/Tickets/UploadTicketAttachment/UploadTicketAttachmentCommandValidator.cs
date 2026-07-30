using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.UploadTicketAttachment;

public sealed class UploadTicketAttachmentCommandValidator : AbstractValidator<UploadTicketAttachmentCommand>
{
    public UploadTicketAttachmentCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.UploadedByUserId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.ContentType).NotEmpty();
        RuleFor(x => x.FileSizeBytes).GreaterThan(0);
    }
}
