namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>
/// Tab names of the Products workbook. Besides the fixed tabs, every other tab is read as a category tab (its name is the
/// category), so the template and the export draw one tab per category with <see cref="TryCategoryTabName"/>.
/// </summary>
internal static class CatalogSheetNames
{
    /// <summary>Tabs that mean something other than "a category's products".</summary>
    public static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "Products", "Variant Groups", "Variant Options", "Variants", "Inventory Codes", "Inventory", "Codes",
        "Categories", "Collections", "Instructions", "README", "Notes", "Lists", "Summary",
        "Pricing", "Stock", "Product Filters",
    };

    /// <summary>Excel allows at most 31 characters and none of <c>[ ] : * ? / \</c>; a name is not changed to fit, since the tab name IS the category.</summary>
    private static readonly char[] Forbidden = ['[', ']', ':', '*', '?', '/', '\\'];

    /// <summary>
    /// The tab name for a category, or null when it cannot be one (empty, too long, forbidden characters, a reserved name or
    /// a quote at the ends). Such a category's products simply stay on the "Products" tab with their category in a column.
    /// </summary>
    public static string? TryCategoryTabName(string? categoryName)
    {
        var name = categoryName?.Trim();
        if (string.IsNullOrEmpty(name)
            || name.Length > 31
            || name.IndexOfAny(Forbidden) >= 0
            || name.StartsWith('\'')
            || name.EndsWith('\'')
            || Reserved.Contains(name))
        {
            return null;
        }

        return name;
    }

    /// <summary>The category tabs for these names: valid, unique (Excel compares tab names ignoring case) and capped so a huge catalog cannot make a workbook of hundreds of tabs.</summary>
    public static IReadOnlyList<string> CategoryTabs(IEnumerable<string> categoryNames, int max = 60)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tabs = new List<string>();
        foreach (var candidate in categoryNames)
        {
            var name = TryCategoryTabName(candidate);
            if (name is not null && tabs.Count < max && seen.Add(name))
            {
                tabs.Add(name);
            }
        }

        return tabs;
    }
}
