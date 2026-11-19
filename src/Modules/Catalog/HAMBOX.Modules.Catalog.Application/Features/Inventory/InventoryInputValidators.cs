using FluentValidation;
using HAMBOX.Modules.Catalog.Application.Features.Products;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

// Input rules for the inventory commands that take free text. The limits mirror the database columns, so a value that
// would otherwise fail inside SaveChanges (a 500) is refused up front as a 400 with a readable message.

/// <summary>Limits that mirror the inventory columns; kept in one place so validators and schema stay in step.</summary>
internal static class InventoryLimits
{
    public const int Name = 200;
    public const int Reason = 500;
    public const int DescriptionHtml = 20_000;
    public const int Notes = 2_000;
}

public sealed class CreateOptionGroupTemplateCommandValidator : AbstractValidator<CreateOptionGroupTemplateCommand>
{
    public CreateOptionGroupTemplateCommandValidator() =>
        RuleFor(x => x.Name).NotEmpty().MaximumLength(InventoryLimits.Name);
}

public sealed class SaveOptionGroupAsTemplateCommandValidator : AbstractValidator<SaveOptionGroupAsTemplateCommand>
{
    public SaveOptionGroupAsTemplateCommandValidator()
    {
        RuleFor(x => x.OptionGroupId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(InventoryLimits.Name);
    }
}

public sealed class UpdateOptionGroupTemplateCommandValidator : AbstractValidator<UpdateOptionGroupTemplateCommand>
{
    public UpdateOptionGroupTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(InventoryLimits.Name);
    }
}

public sealed class CreateOptionDescriptionTemplateCommandValidator : AbstractValidator<CreateOptionDescriptionTemplateCommand>
{
    public CreateOptionDescriptionTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(InventoryLimits.Name);
        RuleFor(x => x.DescriptionHtml).MaximumLength(InventoryLimits.DescriptionHtml);
    }
}

public sealed class UpdateOptionDescriptionTemplateCommandValidator : AbstractValidator<UpdateOptionDescriptionTemplateCommand>
{
    public UpdateOptionDescriptionTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(InventoryLimits.Name);
        RuleFor(x => x.DescriptionHtml).MaximumLength(InventoryLimits.DescriptionHtml);
    }
}

public sealed class UpdateProductOptionGroupCommandValidator : AbstractValidator<UpdateProductOptionGroupCommand>
{
    public UpdateProductOptionGroupCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(InventoryLimits.Name);
        RuleFor(x => x.DescriptionHtml).MaximumLength(InventoryLimits.DescriptionHtml);
    }
}

public sealed class SetVariantDescriptionCommandValidator : AbstractValidator<SetVariantDescriptionCommand>
{
    public SetVariantDescriptionCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.DescriptionHtml).MaximumLength(InventoryLimits.DescriptionHtml);
    }
}

public sealed class UpdateProductOptionCommandValidator : AbstractValidator<UpdateProductOptionCommand>
{
    public UpdateProductOptionCommandValidator()
    {
        RuleFor(x => x.OptionId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(InventoryLimits.Name);
        RuleFor(x => x.DescriptionHtml).MaximumLength(InventoryLimits.DescriptionHtml);
    }
}

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(InventoryLimits.Name);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes).MaximumLength(InventoryLimits.Notes);
    }
}

public sealed class CreateInventoryBatchCommandValidator : AbstractValidator<CreateInventoryBatchCommand>
{
    public CreateInventoryBatchCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(InventoryLimits.Name);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.PurchaseCost).InclusiveBetween(0, ProductPublishGuard.MaxPrice);
        RuleFor(x => x.ExpectedMargin).InclusiveBetween(-1000m, 1000m).When(x => x.ExpectedMargin.HasValue);
        RuleFor(x => x.Notes).MaximumLength(InventoryLimits.Notes);
    }
}

public sealed class MarkInventoryCodeFailedCommandValidator : AbstractValidator<MarkInventoryCodeFailedCommand>
{
    public MarkInventoryCodeFailedCommandValidator()
    {
        RuleFor(x => x.CodeId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(InventoryLimits.Reason);
    }
}

public sealed class SetProductVariantPricesCommandValidator : AbstractValidator<SetProductVariantPricesCommand>
{
    public SetProductVariantPricesCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Value).InclusiveBetween(0, ProductPublishGuard.MaxPrice).When(x => x.Value.HasValue);
    }
}

public sealed class RevealInventoryCodeCommandValidator : AbstractValidator<RevealInventoryCodeCommand>
{
    public RevealInventoryCodeCommandValidator()
    {
        RuleFor(x => x.CodeId).NotEmpty();
        // Emptiness is reported by the handler with its own, more specific error; this only guards the audit column.
        RuleFor(x => x.Reason).MaximumLength(InventoryLimits.Reason);
    }
}
