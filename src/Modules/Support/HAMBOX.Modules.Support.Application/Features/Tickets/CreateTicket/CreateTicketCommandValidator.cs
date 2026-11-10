using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.CreateTicket;

public sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.CustomerUserId).NotEmpty();
        RuleFor(x => x.CreatedByUserId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10_000);
        // Mirror the column widths so an over-long value is a 400, not a database error.
        RuleFor(x => x.CustomerCountry).MaximumLength(100);
        RuleFor(x => x.CustomerBrowser).MaximumLength(200);
        RuleFor(x => x.CustomerDevice).MaximumLength(200);
    }
}
