using HAMBOX.Modules.Catalog.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Resolves primary product image URLs from the catalog database.
/// </summary>
internal static class ProductPrimaryImageResolver
{
    public static async Task<IReadOnlyDictionary<Guid, string?>> GetPrimaryImageUrlsAsync(
        ICatalogDbContext catalogDbContext,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        var images = await catalogDbContext.ProductImages
            .AsNoTracking()
            .Where(image => productIds.Contains(image.ProductId))
            .Select(image => new
            {
                image.ProductId,
                image.Url,
                image.IsPrimary,
                image.DisplayOrder,
            })
            .ToListAsync(cancellationToken);

        return images
            .GroupBy(image => image.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.Url)
                    .FirstOrDefault());
    }
}
