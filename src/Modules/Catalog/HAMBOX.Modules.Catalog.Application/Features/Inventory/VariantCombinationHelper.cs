using HAMBOX.Modules.Catalog.Domain.Inventory;

namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

public static class VariantCombinationHelper
{
    /// <summary>
    /// Recursively expands a product's option-group tree into variant paths. Root-level groups
    /// (ParentOptionId == null) still cross-multiply with each other (matches the old flat
    /// behavior); an option only ever combines with its own child groups, never a sibling
    /// branch's groups, so "Xbox" and "PSN"'s child groups never mix. Every option always
    /// contributes itself to the path — see <see cref="ExpandOption"/>.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<Guid>> Expand(IReadOnlyList<ProductOptionGroup> allGroups)
    {
        var rootGroups = allGroups
            .Where(g => g.ParentOptionId is null)
            .OrderBy(g => g.SortOrder)
            .ToList();

        if (rootGroups.Count == 0 || rootGroups.Any(g => g.Options.Count == 0))
        {
            return [];
        }

        var childGroupsByParentOptionId = allGroups
            .Where(g => g.ParentOptionId is not null)
            .ToLookup(g => g.ParentOptionId!.Value);

        return ExpandGroups(rootGroups, childGroupsByParentOptionId);
    }

    private static IReadOnlyList<IReadOnlyList<Guid>> ExpandGroups(
        IReadOnlyList<ProductOptionGroup> groups,
        ILookup<Guid, ProductOptionGroup> childGroupsByParentOptionId)
    {
        IEnumerable<IReadOnlyList<Guid>> paths = [[]];

        foreach (var group in groups.OrderBy(g => g.SortOrder))
        {
            paths = paths.SelectMany(partial => group.Options
                .OrderBy(option => option.SortOrder)
                .SelectMany(option => ExpandOption(option, childGroupsByParentOptionId))
                .Select(optionPath => (IReadOnlyList<Guid>)partial.Concat(optionPath).ToList()));
        }

        return paths.ToList();
    }

    /// <summary>
    /// Every option always contributes itself to the variant path. If it owns non-empty child
    /// groups, recursion continues into them and the option only ever appears refined by its
    /// children (it is never itself a bare terminal alongside the refined paths). If it has no
    /// child groups, the option is a terminal on its own. This holds regardless of the child
    /// group's <c>IsRequired</c> flag — that flag governs storefront selection UX, not variant
    /// generation.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<Guid>> ExpandOption(
        ProductOption option,
        ILookup<Guid, ProductOptionGroup> childGroupsByParentOptionId)
    {
        var childGroups = childGroupsByParentOptionId[option.Id]
            .Where(g => g.Options.Count > 0)
            .ToList();

        if (childGroups.Count == 0)
        {
            return [[option.Id]];
        }

        return ExpandGroups(childGroups, childGroupsByParentOptionId)
            .Select(childPath => (IReadOnlyList<Guid>)new[] { option.Id }.Concat(childPath).ToList())
            .ToList();
    }

    public static string NormalizeCombinationKey(IEnumerable<Guid> optionIds) =>
        string.Join("|", optionIds.OrderBy(id => id));

    /// <summary>
    /// The full set of normalized combination keys <see cref="Expand"/> would generate for this
    /// product's option-group tree right now — the single authoritative definition of "a complete,
    /// purchasable variant combination" (every reachable group filled, exactly one option each,
    /// regardless of <c>IsRequired</c> — see <see cref="Expand"/>'s own remarks). Compute once per
    /// product and reuse across every variant, rather than re-expanding per variant.
    /// </summary>
    public static HashSet<string> BuildValidCombinationKeys(IReadOnlyList<ProductOptionGroup> allGroups) =>
        Expand(allGroups).Select(NormalizeCombinationKey).ToHashSet();

    /// <summary>
    /// True when <paramref name="optionIds"/> is an exact, complete combination per
    /// <see cref="BuildValidCombinationKeys"/> — a product with no option groups at all has no
    /// combinations to complete, so any (empty) selection is trivially complete.
    /// </summary>
    public static bool IsCompleteCombination(IReadOnlyList<Guid> optionIds, HashSet<string> validCombinationKeys, IReadOnlyList<ProductOptionGroup> allGroups) =>
        allGroups.Count == 0 || validCombinationKeys.Contains(NormalizeCombinationKey(optionIds));

    /// <summary>
    /// Walks the option-group tree along <paramref name="optionIds"/>' own selections and returns
    /// every reachable group (root groups, plus a child group once its parent option is among
    /// <paramref name="optionIds"/>) that has no option selected — i.e. exactly what a variant would
    /// need to add to become a complete combination. Used to build a specific, actionable validation
    /// error; <see cref="IsCompleteCombination"/> remains the authoritative pass/fail check since it
    /// also catches malformed extra/orphaned option ids that this walk wouldn't flag.
    /// </summary>
    public static IReadOnlyList<ProductOptionGroup> FindMissingGroups(
        IReadOnlyList<Guid> optionIds,
        IReadOnlyList<ProductOptionGroup> allGroups)
    {
        var selected = optionIds.ToHashSet();
        var rootGroups = allGroups.Where(g => g.ParentOptionId is null).OrderBy(g => g.SortOrder).ToList();
        var childGroupsByParentOptionId = allGroups
            .Where(g => g.ParentOptionId is not null)
            .ToLookup(g => g.ParentOptionId!.Value);

        var missing = new List<ProductOptionGroup>();
        WalkMissingGroups(rootGroups, selected, childGroupsByParentOptionId, missing);
        return missing;
    }

    private static void WalkMissingGroups(
        IReadOnlyList<ProductOptionGroup> groups,
        HashSet<Guid> selected,
        ILookup<Guid, ProductOptionGroup> childGroupsByParentOptionId,
        List<ProductOptionGroup> missing)
    {
        foreach (var group in groups.Where(g => g.Options.Count > 0))
        {
            var chosenOption = group.Options.FirstOrDefault(o => selected.Contains(o.Id));
            if (chosenOption is null)
            {
                missing.Add(group);
                continue;
            }

            var childGroups = childGroupsByParentOptionId[chosenOption.Id].Where(g => g.Options.Count > 0).ToList();
            if (childGroups.Count > 0)
            {
                WalkMissingGroups(childGroups, selected, childGroupsByParentOptionId, missing);
            }
        }
    }

    public static string BuildSku(string productPrefix, IReadOnlyList<string> optionValues, HashSet<string> usedSkus)
    {
        var slugParts = optionValues
            .Select(Slugify)
            .Where(part => part.Length > 0)
            .ToList();

        var baseSku = slugParts.Count == 0
            ? productPrefix
            : $"{productPrefix}-{string.Join("-", slugParts)}";

        baseSku = TruncateSku(baseSku);

        if (!usedSkus.Contains(baseSku))
        {
            usedSkus.Add(baseSku);
            return baseSku;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = TruncateSku($"{baseSku}-{suffix}");
            if (!usedSkus.Contains(candidate))
            {
                usedSkus.Add(candidate);
                return candidate;
            }

            suffix++;
        }
    }

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToUpperInvariant()
            .Where(ch => char.IsLetterOrDigit(ch))
            .ToArray();

        return new string(chars);
    }

    private static string TruncateSku(string sku) => sku.Length <= 64 ? sku : sku[..64];
}
