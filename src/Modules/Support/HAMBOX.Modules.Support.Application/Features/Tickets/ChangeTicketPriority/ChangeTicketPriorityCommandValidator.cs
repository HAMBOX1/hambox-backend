using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ChangeTicketPriority;

public sealed class ChangeTicketPriorityCommandValidator : AbstractValidator<ChangeTicketPriorityCommand>
{
    public ChangeTicketPriorityCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.ChangedByUserId).NotEmpty();
    }
}
