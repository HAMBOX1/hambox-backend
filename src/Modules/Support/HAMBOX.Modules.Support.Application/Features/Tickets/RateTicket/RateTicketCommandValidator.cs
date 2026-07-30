using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.RateTicket;

public sealed class RateTicketCommandValidator : AbstractValidator<RateTicketCommand>
{
    public RateTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.CustomerUserId).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(2_000);
    }
}
