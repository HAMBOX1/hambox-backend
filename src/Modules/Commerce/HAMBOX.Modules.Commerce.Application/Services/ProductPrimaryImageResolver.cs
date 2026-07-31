using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Services;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Resolves display image URLs for products from the catalog database, for Commerce
/// surfaces (wishlist, library, dashboard, admin order detail) that only need the URL.
/// </summary>
/// <remarks>
/// Delegates to <see cref="ProductDisplayImageResolver"/> (Catalog's shared fallback chain:
/// product image &gt; category image &gt; ancestor category image &gt; placeholder) so every
/// caller here automatically gets a real, non-null image without re-implementing the chain.
/// </remarks>
internal static class ProductPrimaryImageResolver
{
    public static async Task<IReadOnlyDictionary<Guid, string?>> GetPrimaryImageUrlsAsync(
        ICatalogDbContext catalogDbContext,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var displayImages = await ProductDisplayImageResolver.ResolveManyAsync(catalogDbContext, productIds, cancellationToken);

        return displayImages.ToDictionary(
            entry => entry.Key,
            entry => (string?)entry.Value.Url);
    }
}
