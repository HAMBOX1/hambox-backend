using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.MergeTickets;

public sealed class MergeTicketsCommandValidator : AbstractValidator<MergeTicketsCommand>
{
    public MergeTicketsCommandValidator()
    {
        RuleFor(x => x.SourceTicketId).NotEmpty();
        RuleFor(x => x.TargetTicketId).NotEmpty();
        RuleFor(x => x.MergedByUserId).NotEmpty();
        RuleFor(x => x).Must(x => x.SourceTicketId != x.TargetTicketId)
            .WithMessage("A ticket cannot be merged into itself.");
    }
}
