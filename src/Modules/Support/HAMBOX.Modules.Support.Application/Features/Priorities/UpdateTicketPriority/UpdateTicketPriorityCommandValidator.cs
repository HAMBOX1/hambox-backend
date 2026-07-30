using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.UpdateTicketPriority;

public sealed class UpdateTicketPriorityCommandValidator : AbstractValidator<UpdateTicketPriorityCommand>
{
    public UpdateTicketPriorityCommandValidator()
    {
        RuleFor(x => x.PriorityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(20);
    }
}
