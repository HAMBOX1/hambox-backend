using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.MarkTicketMessageRead;

public sealed class MarkTicketMessageReadCommandValidator : AbstractValidator<MarkTicketMessageReadCommand>
{
    public MarkTicketMessageReadCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.ReaderUserId).NotEmpty();
    }
}
