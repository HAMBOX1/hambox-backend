using HAMBOX.Modules.Catalog.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Services;

/// <summary>
/// Names for where a product's <see cref="ProductDisplayImage.Url"/> came from.
/// </summary>
public static class ProductDisplayImageSource
{
    public const string Product = "Product";
    public const string Category = "Category";
    public const string ParentCategory = "ParentCategory";
    public const string Placeholder = "Placeholder";
}

/// <summary>
/// A resolved image for storefront/admin display, plus where it came from (for admin UX).
/// </summary>
public sealed record ProductDisplayImage(string Url, string Source);

/// <summary>
/// Resolves the image every product should show even when the merchant never uploaded one:
/// the product's own image, else its category's image, else the nearest ancestor category's
/// image, else a global placeholder. The single source of truth for that fallback chain —
/// every DTO that carries a product image (Catalog's own <c>ProductDto</c>, and Commerce's
/// wishlist/library/dashboard/order DTOs via <see cref="ResolveManyAsync"/>) routes through here
/// instead of re-implementing it.
/// </summary>
public static class ProductDisplayImageResolver
{
    /// <summary>
    /// Frontend-served (not <c>/uploads</c>) placeholder asset — every product always resolves
    /// to a real, non-null image URL.
    /// </summary>
    public const string PlaceholderImageUrl = "/assets/images/placeholders/product.svg";

    /// <summary>
    /// Resolution order: product's own image (primary, or first by display order) &gt;
    /// the product's own category's image &gt; the nearest ancestor category's image &gt; placeholder.
    /// </summary>
    public static ProductDisplayImage Resolve(
        string? productImageUrl,
        string? categoryOwnImageUrl,
        string? categoryEffectiveImageUrl)
    {
        if (!string.IsNullOrWhiteSpace(productImageUrl))
        {
            return new ProductDisplayImage(productImageUrl, ProductDisplayImageSource.Product);
        }

        if (!string.IsNullOrWhiteSpace(categoryOwnImageUrl))
        {
            return new ProductDisplayImage(categoryOwnImageUrl, ProductDisplayImageSource.Category);
        }

        if (!string.IsNullOrWhiteSpace(categoryEffectiveImageUrl))
        {
            return new ProductDisplayImage(categoryEffectiveImageUrl, ProductDisplayImageSource.ParentCategory);
        }

        return new ProductDisplayImage(PlaceholderImageUrl, ProductDisplayImageSource.Placeholder);
    }

    /// <summary>
    /// Batch-resolves display images for a set of products in a single query (no N+1) —
    /// for cross-module consumers (Commerce) that only need the URL, not the source.
    /// Every requested id is present in the result, even if the product no longer exists.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ProductDisplayImage>> ResolveManyAsync(
        ICatalogDbContext catalogDbContext,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, ProductDisplayImage>();
        }

        var rows = await catalogDbContext.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                ProductImageUrl = p.Images.FirstOrDefault(i => i.IsPrimary)!.Url
                    ?? p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                CategoryOwnImageUrl = catalogDbContext.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.ImageUrl).FirstOrDefault(),
                CategoryEffectiveImageUrl = catalogDbContext.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.EffectiveImageUrl).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.Id,
            row => Resolve(row.ProductImageUrl, row.CategoryOwnImageUrl, row.CategoryEffectiveImageUrl));
    }
}
