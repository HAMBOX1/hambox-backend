using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.DeleteTicket;

public sealed class DeleteTicketCommandValidator : AbstractValidator<DeleteTicketCommand>
{
    public DeleteTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.DeletedByUserId).NotEmpty();
    }
}
