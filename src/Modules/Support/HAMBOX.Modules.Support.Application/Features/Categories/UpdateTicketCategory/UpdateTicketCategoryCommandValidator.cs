using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.Categories.UpdateTicketCategory;

public sealed class UpdateTicketCategoryCommandValidator : AbstractValidator<UpdateTicketCategoryCommand>
{
    public UpdateTicketCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Icon).NotEmpty().MaximumLength(50);
    }
}
