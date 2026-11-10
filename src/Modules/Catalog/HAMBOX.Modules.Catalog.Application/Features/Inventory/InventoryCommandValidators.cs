using FluentValidation;
using HAMBOX.Modules.Catalog.Application.Features.Products;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

public sealed class CreateProductOptionGroupCommandValidator : AbstractValidator<CreateProductOptionGroupCommand>
{
    public CreateProductOptionGroupCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Key).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateProductOptionCommandValidator : AbstractValidator<CreateProductOptionCommand>
{
    public CreateProductOptionCommandValidator()
    {
        RuleFor(x => x.OptionGroupId).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateProductVariantCommandValidator : AbstractValidator<CreateProductVariantCommand>
{
    public CreateProductVariantCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
        // A zero override would sell the variant for free; "use the product price" is expressed as null, never 0.
        RuleFor(x => x.PriceOverride).GreaterThan(0).LessThanOrEqualTo(ProductPublishGuard.MaxPrice).When(x => x.PriceOverride.HasValue);
        RuleFor(x => x.ComparePrice).InclusiveBetween(0, ProductPublishGuard.MaxPrice).When(x => x.ComparePrice.HasValue);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductVariantCommandValidator : AbstractValidator<UpdateProductVariantCommand>
{
    public UpdateProductVariantCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
        // A zero override would sell the variant for free; "use the product price" is expressed as null, never 0.
        RuleFor(x => x.PriceOverride).GreaterThan(0).LessThanOrEqualTo(ProductPublishGuard.MaxPrice).When(x => x.PriceOverride.HasValue);
        RuleFor(x => x.ComparePrice).InclusiveBetween(0, ProductPublishGuard.MaxPrice).When(x => x.ComparePrice.HasValue);
        RuleFor(x => x.CostPrice).InclusiveBetween(0, ProductPublishGuard.MaxPrice).When(x => x.CostPrice.HasValue);
        RuleFor(x => x.MemberPrice).GreaterThan(0).LessThanOrEqualTo(ProductPublishGuard.MaxPrice).When(x => x.MemberPrice.HasValue);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0);
    }
}

public sealed class BulkUpdateProductVariantsCommandValidator : AbstractValidator<BulkUpdateProductVariantsCommand>
{
    public BulkUpdateProductVariantsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.VariantIds).NotEmpty();
        // A zero override would sell the variant for free; "use the product price" is expressed as null, never 0.
        RuleFor(x => x.PriceOverride).GreaterThan(0).LessThanOrEqualTo(ProductPublishGuard.MaxPrice).When(x => x.PriceOverride.HasValue);
    }
}

public sealed class BulkDisableInventoryCodesCommandValidator : AbstractValidator<BulkDisableInventoryCodesCommand>
{
    public BulkDisableInventoryCodesCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.CodeIds).NotEmpty();
    }
}

public sealed class BulkDeleteInventoryCodesCommandValidator : AbstractValidator<BulkDeleteInventoryCodesCommand>
{
    public BulkDeleteInventoryCodesCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.CodeIds).NotEmpty();
    }
}
