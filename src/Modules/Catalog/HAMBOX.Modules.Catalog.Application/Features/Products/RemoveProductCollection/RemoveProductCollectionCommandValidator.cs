using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.RemoveProductCollection;

public class RemoveProductCollectionCommandValidator : AbstractValidator<RemoveProductCollectionCommand>
{
    public RemoveProductCollectionCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.CollectionId).NotEmpty();
    }
}
