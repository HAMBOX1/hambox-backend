using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Services;

/// <summary>
/// Keeps <see cref="Category.EffectiveImageUrl"/> (this category's own image, or the nearest
/// ancestor's) in sync whenever a category's own image or parent changes.
/// </summary>
/// <remarks>
/// Denormalizing this onto the row lets product queries resolve "parent category image"
/// with a single join instead of walking the category tree per request. The cascade below
/// only runs on the rare admin write path (image upload/removal, create, reparent), never
/// on a read.
/// </remarks>
internal static class CategoryImageResolution
{
    /// <summary>
    /// Recomputes <paramref name="category"/>'s own <see cref="Category.EffectiveImageUrl"/>
    /// from its current <see cref="Category.ImageUrl"/>/parent, then propagates that value
    /// down to every descendant that doesn't have its own image.
    /// </summary>
    public static async Task ApplyAndPropagateAsync(
        ICatalogDbContext db,
        Category category,
        CancellationToken cancellationToken)
    {
        var effective = category.ImageUrl ?? await ResolveParentEffectiveImageAsync(db, category.ParentId, cancellationToken);
        category.SetEffectiveImageUrl(effective);

        // Every descendant reached below lacks its own image (checked before enqueueing), so
        // they all inherit the same resolved value as `category` itself — not a per-level walk.
        var queue = new Queue<Guid>();
        queue.Enqueue(category.Id);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();

            var children = await db.Categories
                .Where(c => c.ParentId == parentId)
                .ToListAsync(cancellationToken);

            foreach (var child in children)
            {
                if (child.ImageUrl is not null)
                {
                    // Owns its own image — its subtree already inherits from it, unaffected.
                    continue;
                }

                child.SetEffectiveImageUrl(effective);
                queue.Enqueue(child.Id);
            }
        }
    }

    /// <summary>
    /// Resolves the effective image a brand-new child of <paramref name="parentId"/> would inherit.
    /// </summary>
    public static async Task<string?> ResolveParentEffectiveImageAsync(
        ICatalogDbContext db,
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return null;
        }

        return await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == parentId)
            .Select(c => c.EffectiveImageUrl)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
