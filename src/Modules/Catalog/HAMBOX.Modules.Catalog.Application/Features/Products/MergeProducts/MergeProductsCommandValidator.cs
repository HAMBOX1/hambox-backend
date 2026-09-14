using System.Linq;
using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.MergeProducts;

public sealed class MergeProductsCommandValidator : AbstractValidator<MergeProductsCommand>
{
    public MergeProductsCommandValidator()
    {
        RuleFor(x => x.TargetProductId).NotEmpty();

        RuleFor(x => x.SourceProductIds)
            .NotEmpty()
            .WithMessage("Select at least one other product to merge into the target.");

        RuleFor(x => x.SourceProductIds)
            .Must(ids => ids.Count == ids.Distinct().Count())
            .WithMessage("The selected products cannot contain duplicates.")
            .When(x => x.SourceProductIds is { Count: > 0 });

        RuleFor(x => x)
            .Must(x => !x.SourceProductIds.Contains(x.TargetProductId))
            .WithMessage("The target product cannot also be one of the merged source products.")
            .When(x => x.SourceProductIds is { Count: > 0 });
    }
}
