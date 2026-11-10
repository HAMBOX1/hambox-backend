using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products;

/// <summary>
/// The single "can this product be sold?" rule used by every path that makes a product Active
/// (publish, update-to-Active, create-as-Active, bulk publish) so a $0 or unbuyable product can't go live.
/// </summary>
public static class ProductPublishGuard
{
    /// <summary>Hard ceiling for any price a product or price adjustment may produce (USD).</summary>
    public const decimal MaxPrice = 10_000_000m;

    public static async Task<Result> CheckAsync(ICatalogDbContext db, Product product, CancellationToken cancellationToken)
    {
        // Publishing activates the product's variants, so any non-archived variant makes it sellable.
        var variantPrices = await db.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == product.Id && !v.IsDeleted && v.Status != ProductVariantStatus.Archived)
            .Select(v => v.PriceOverride)
            .ToListAsync(cancellationToken);

        if (variantPrices.Count == 0)
        {
            return Result.Failure(CatalogErrors.ProductNotPublishableNoVariant);
        }

        return product.Price > 0 || variantPrices.Any(price => price > 0)
            ? Result.Success()
            : Result.Failure(CatalogErrors.ProductNotPublishablePrice);
    }
}
