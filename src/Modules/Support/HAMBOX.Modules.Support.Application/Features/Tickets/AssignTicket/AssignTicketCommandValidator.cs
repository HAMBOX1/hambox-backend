using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.AssignTicket;

public sealed class AssignTicketCommandValidator : AbstractValidator<AssignTicketCommand>
{
    public AssignTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.AgentUserId).NotEmpty();
        RuleFor(x => x.AssignedByUserId).NotEmpty();
    }
}
