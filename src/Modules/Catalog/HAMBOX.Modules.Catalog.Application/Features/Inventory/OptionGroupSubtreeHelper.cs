using HAMBOX.Modules.Catalog.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// Shared recursive walk over an option's child-option-group subtree, used by the option/option-group
/// delete handlers to extend their existing manual-cascade pattern below the direct children.
/// </summary>
internal static class OptionGroupSubtreeHelper
{
    public static async Task<List<Guid>> CollectDescendantOptionIdsAsync(ICatalogDbContext db, Guid optionId, CancellationToken cancellationToken)
    {
        var childGroups = await db.ProductOptionGroups
            .Where(g => g.ParentOptionId == optionId)
            .Include(g => g.Options)
            .ToListAsync(cancellationToken);

        var descendantIds = new List<Guid>();
        foreach (var group in childGroups)
        {
            foreach (var option in group.Options)
            {
                descendantIds.Add(option.Id);
                descendantIds.AddRange(await CollectDescendantOptionIdsAsync(db, option.Id, cancellationToken));
            }
        }

        return descendantIds;
    }

    public static async Task DeleteOptionDescendantsAsync(ICatalogDbContext db, Guid optionId, CancellationToken cancellationToken)
    {
        var childGroups = await db.ProductOptionGroups
            .Where(g => g.ParentOptionId == optionId)
            .Include(g => g.Options)
            .ToListAsync(cancellationToken);

        foreach (var group in childGroups)
        {
            foreach (var option in group.Options.ToList())
            {
                await DeleteOptionDescendantsAsync(db, option.Id, cancellationToken);
            }

            db.ProductOptions.RemoveRange(group.Options);
            db.ProductOptionGroups.Remove(group);
        }
    }
}
