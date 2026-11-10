using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;

public class AdjustProductPriceCommandValidator : AbstractValidator<AdjustProductPriceCommand>
{
    public AdjustProductPriceCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Value).InclusiveBetween(0, ProductPublishGuard.MaxPrice);
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => x.Mode == PriceAdjustmentMode.DecreasePercent);
    }
}
