using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ReplyToTicket;

public sealed class ReplyToTicketCommandValidator : AbstractValidator<ReplyToTicketCommand>
{
    public ReplyToTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.AuthorUserId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10_000);
    }
}
