using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.ChangeProductCategory;

public class ChangeProductCategoryCommandValidator : AbstractValidator<ChangeProductCategoryCommand>
{
    public ChangeProductCategoryCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}
