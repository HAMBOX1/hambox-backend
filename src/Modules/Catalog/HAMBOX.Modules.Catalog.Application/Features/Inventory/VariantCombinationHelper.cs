namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

internal static class VariantCombinationHelper
{
    public static IReadOnlyList<IReadOnlyList<Guid>> BuildCartesianProduct(
        IReadOnlyList<IReadOnlyList<Guid>> optionIdsPerGroup)
    {
        if (optionIdsPerGroup.Count == 0)
        {
            return [];
        }

        if (optionIdsPerGroup.Any(group => group.Count == 0))
        {
            return [];
        }

        IEnumerable<IReadOnlyList<Guid>> combinations = [[]];

        foreach (var group in optionIdsPerGroup)
        {
            combinations = combinations.SelectMany(
                partial => group.Select(optionId => partial.Append(optionId).ToList().AsReadOnly()));
        }

        return combinations.ToList();
    }

    public static string NormalizeCombinationKey(IEnumerable<Guid> optionIds) =>
        string.Join("|", optionIds.OrderBy(id => id));

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
