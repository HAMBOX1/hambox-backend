using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Categories.CreateTicketCategory;

public sealed class CreateTicketCategoryCommandValidator : AbstractValidator<CreateTicketCategoryCommand>
{
    public CreateTicketCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Icon).NotEmpty().MaximumLength(50);
    }
}
