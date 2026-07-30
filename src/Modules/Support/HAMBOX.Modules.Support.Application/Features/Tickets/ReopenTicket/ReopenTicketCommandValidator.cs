using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ReopenTicket;

public sealed class ReopenTicketCommandValidator : AbstractValidator<ReopenTicketCommand>
{
    public ReopenTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.RequestedByUserId).NotEmpty();
    }
}
