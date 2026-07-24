using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.AssignProductCollection;

public class AssignProductCollectionCommandValidator : AbstractValidator<AssignProductCollectionCommand>
{
    public AssignProductCollectionCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.CollectionId).NotEmpty();
    }
}
