using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.GetTickets;

public sealed class GetTicketsQueryValidator : AbstractValidator<GetTicketsQuery>
{
    public GetTicketsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
