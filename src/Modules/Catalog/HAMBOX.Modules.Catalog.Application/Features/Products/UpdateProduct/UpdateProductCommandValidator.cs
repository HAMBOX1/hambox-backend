using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.UpdateProduct;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameAr).MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).InclusiveBetween(0, ProductPublishGuard.MaxPrice);
        RuleFor(x => x.Price)
            .Must(price => decimal.Round(price, 2) == price)
            .WithMessage("Price can have at most 2 decimal places.");
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.AdditionalCategoryIds)
            .Must(ids => ids is null || ids.Count == ids.Distinct().Count())
            .WithMessage("Additional categories cannot contain duplicates.");
        RuleFor(x => x.AdditionalCategoryIds)
            .Must((command, ids) => ids is null || !ids.Contains(command.CategoryId))
            .WithMessage("The primary category cannot also be selected as an additional category.");
    }
}
