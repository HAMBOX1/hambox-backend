using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tags.UpdateTicketTag;

public sealed class UpdateTicketTagCommandValidator : AbstractValidator<UpdateTicketTagCommand>
{
    public UpdateTicketTagCommandValidator()
    {
        RuleFor(x => x.TagId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(20);
    }
}
