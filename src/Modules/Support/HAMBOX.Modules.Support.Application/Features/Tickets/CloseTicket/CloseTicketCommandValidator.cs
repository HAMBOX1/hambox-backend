using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.CloseTicket;

public sealed class CloseTicketCommandValidator : AbstractValidator<CloseTicketCommand>
{
    public CloseTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.RequestedByUserId).NotEmpty();
    }
}
