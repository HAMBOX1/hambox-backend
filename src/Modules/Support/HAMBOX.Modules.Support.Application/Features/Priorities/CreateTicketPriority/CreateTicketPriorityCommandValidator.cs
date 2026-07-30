using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.CreateTicketPriority;

public sealed class CreateTicketPriorityCommandValidator : AbstractValidator<CreateTicketPriorityCommand>
{
    public CreateTicketPriorityCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(20);
        RuleFor(x => x.SlaFirstResponseMinutes).GreaterThan(0).When(x => x.SlaFirstResponseMinutes is not null);
        RuleFor(x => x.SlaResolutionMinutes).GreaterThan(0).When(x => x.SlaResolutionMinutes is not null);
    }
}
