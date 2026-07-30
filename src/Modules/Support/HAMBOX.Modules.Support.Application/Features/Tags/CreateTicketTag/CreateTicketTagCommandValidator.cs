using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tags.CreateTicketTag;

public sealed class CreateTicketTagCommandValidator : AbstractValidator<CreateTicketTagCommand>
{
    public CreateTicketTagCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(20);
    }
}
