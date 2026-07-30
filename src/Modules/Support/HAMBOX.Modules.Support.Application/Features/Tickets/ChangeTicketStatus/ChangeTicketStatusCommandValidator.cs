using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ChangeTicketStatus;

public sealed class ChangeTicketStatusCommandValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.ChangedByUserId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}
