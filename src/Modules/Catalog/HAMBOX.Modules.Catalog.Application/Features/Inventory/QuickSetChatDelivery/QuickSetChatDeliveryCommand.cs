using FluentValidation;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Products.MergeProducts;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory.QuickSetChatDelivery;

/// <summary>
/// The catalog list's one-click "set as On-Delivery" action — for a simple product (no variants, or
/// exactly one), configures it for <see cref="FulfillmentMode.ChatDelivery"/> with a given capacity in
/// a single call instead of the admin having to open the product, add a variant, and set fulfillment
/// mode separately. Deliberately refuses a product with more than one variant — reconfiguring the
/// right one is ambiguous from the catalog list; per-variant instructions/fulfillment controls live in
/// the variant manager instead.
/// </summary>
public sealed record QuickSetChatDeliveryCommand(Guid ProductId, int Capacity) : IRequest<Result>;

public sealed class QuickSetChatDeliveryCommandValidator : AbstractValidator<QuickSetChatDeliveryCommand>
{
    public QuickSetChatDeliveryCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0);
    }
}

internal sealed class QuickSetChatDeliveryCommandHandler(ICatalogDbContext db) : IRequestHandler<QuickSetChatDeliveryCommand, Result>
{
    public async Task<Result> Handle(QuickSetChatDeliveryCommand request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        var variants = await db.ProductVariants
            .Where(v => v.ProductId == request.ProductId && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        if (variants.Count > 1)
        {
            return Result.Failure(CatalogErrors.ProductHasMultipleVariantsForQuickAction);
        }

        if (variants.Count == 1)
        {
            var variant = variants[0];
            variant.SetFulfillmentMode(FulfillmentMode.ChatDelivery);
            variant.SetManualDeliveryCapacity(request.Capacity);
        }
        else
        {
            var baseSku = SkuSlugifier.Slugify(product.NameEn);
            var skuTaken = await db.ProductVariants.AnyAsync(v => v.Sku == baseSku && !v.IsDeleted, cancellationToken);
            var sku = skuTaken ? $"{baseSku}-{Guid.NewGuid().ToString("N")[..6]}" : baseSku;

            var variant = ProductVariant.Create(request.ProductId, sku);
            variant.SetOptions([]);
            variant.Activate();
            variant.SetFulfillmentMode(FulfillmentMode.ChatDelivery);
            variant.SetManualDeliveryCapacity(request.Capacity);
            db.ProductVariants.Add(variant);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
