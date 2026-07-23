using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.NameAr).MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DescriptionAr).MaximumLength(2000);
        RuleFor(x => x.DescriptionEn).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.AdditionalCategoryIds)
            .Must(ids => ids is null || ids.Count == ids.Distinct().Count())
            .WithMessage("Additional categories cannot contain duplicates.");
        RuleFor(x => x.AdditionalCategoryIds)
            .Must((command, ids) => ids is null || !ids.Contains(command.CategoryId))
            .WithMessage("The primary category cannot also be selected as an additional category.");
    }
}
