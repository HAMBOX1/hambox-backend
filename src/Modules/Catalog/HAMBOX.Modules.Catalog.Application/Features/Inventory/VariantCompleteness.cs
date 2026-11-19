using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// "Is this variant a complete, sellable combination?" — exactly one option from every option group that applies to it.
/// A variant that skips a group (e.g. Region + Type but no Duration) is only a half-choice a customer passes through while
/// picking; it must never be sold or stocked.
/// </summary>
public static class VariantCompleteness
{
    public static bool IsComplete(IReadOnlyList<Guid> optionIds, IReadOnlyList<ProductOptionGroup> groups, HashSet<string> validKeys)
    {
        if (groups.Count == 0)
        {
            return true;
        }

        // Groups exist but none can produce a combination yet (no values): nothing to complete, only an option-less variant fits.
        if (validKeys.Count == 0)
        {
            return optionIds.Count == 0;
        }

        return validKeys.Contains(VariantCombinationHelper.NormalizeCombinationKey(optionIds));
    }

    /// <summary>Loads the product's option groups and checks one variant's options against them.</summary>
    public static async Task<bool> IsCompleteAsync(
        ICatalogDbContext db, Guid productId, IReadOnlyList<Guid> optionIds, CancellationToken cancellationToken)
    {
        var groups = await db.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .Where(g => g.ProductId == productId)
            .ToListAsync(cancellationToken);

        return IsComplete(optionIds, groups, VariantCombinationHelper.BuildValidCombinationKeys(groups));
    }

    /// <summary>"Region: Turkey", "Type: Essential"... in group order, for showing a combination in the admin.</summary>
    public static IReadOnlyList<string> Labels(IReadOnlyList<Guid> optionIds, IReadOnlyList<ProductOptionGroup> groups)
    {
        var chosen = optionIds.ToHashSet();
        var labels = new List<string>();
        foreach (var group in groups.OrderBy(g => g.SortOrder))
        {
            var option = group.Options.FirstOrDefault(o => chosen.Contains(o.Id));
            if (option is not null)
            {
                labels.Add($"{group.DisplayName}: {option.Label}");
            }
        }

        return labels;
    }
}
