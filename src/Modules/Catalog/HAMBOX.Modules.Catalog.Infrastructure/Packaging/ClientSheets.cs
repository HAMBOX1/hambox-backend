using System.Globalization;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>
/// Reads the client's five sheet layouts (HX_Categories, HX_Products, HX_Pricing, HX_Stock) into the shared
/// <see cref="ParsedCatalogPackage"/> shape. A sheet is told apart from the older layouts by one header only the client's
/// has (<c>CategoryKey</c>, <c>RecordType</c>, <c>StockData</c>), so older files keep importing.
/// </summary>
internal static class ClientSheets
{
    private static readonly char[] ListSeparators = [';', '|', ','];

    public static bool IsCategorySheet(List<Dictionary<string, string>> rows) => rows.Count > 0 && rows[0].ContainsKey("CategoryKey");

    public static bool IsProductSheet(List<Dictionary<string, string>> rows) =>
        rows.Count > 0 && (rows[0].ContainsKey("RecordType") || rows[0].ContainsKey("ParentSKU"));

    public static bool IsStockSheet(List<Dictionary<string, string>> rows) => rows.Count > 0 && rows[0].ContainsKey("StockData");

    private static string Get(Dictionary<string, string> row, params string[] keys) =>
        keys.Select(k => row.GetValueOrDefault(k, string.Empty)).FirstOrDefault(v => v.Length > 0) ?? string.Empty;

    private static List<string> List(string value) =>
        value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // ---------- Categories ----------

    /// <summary>
    /// A Public row is a storefront category; an Internal row is one of our internal (admin-only) categories, which the system
    /// calls collections: they are never shown to customers and a product can be filed under any number of them.
    /// </summary>
    public static (List<ParsedCategoryRow> Categories, List<ParsedCollectionRow> Collections) Categories(List<Dictionary<string, string>> rows)
    {
        static bool IsInternal(Dictionary<string, string> r) => Get(r, "CategoryType").Equals("Internal", StringComparison.OrdinalIgnoreCase);

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slugs = rows.Select(r => Get(r, "Slug")).ToList();
        foreach (var slug in slugs.Where(s => s.Length > 0))
        {
            taken.Add(slug);
        }

        var keyToSlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var keyToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            if (!IsInternal(rows[i]) && slugs[i].Length == 0 && Get(rows[i], "NameEN").Length > 0)
            {
                slugs[i] = CatalogSlug.Unique(Get(rows[i], "NameEN"), null, taken);
            }

            var key = Get(rows[i], "CategoryKey");
            if (key.Length > 0)
            {
                keyToSlug[key] = slugs[i];
                keyToName[key] = Get(rows[i], "NameEN");
            }
        }

        var collections = rows
            .Where(IsInternal)
            .Select((r, i) =>
            {
                var parent = Get(r, "ParentCategoryKey");
                return new ParsedCollectionRow(
                    i + 1, Get(r, "NameEN"), CatalogImportParser.NullIfEmpty(Get(r, "DescriptionEN")), null, null,
                    parent.Length == 0 ? null : keyToName.GetValueOrDefault(parent, parent),
                    CatalogImportParser.ParseInt(Get(r, "SortOrder"), 0));
            })
            .ToList();

        var categories = rows.Select((r, i) => (r, i)).Where(x => !IsInternal(x.r)).Select(x =>
        {
            var (r, i) = x;
            var status = CatalogImportParser.NullIfEmpty(Get(r, "Status"));
            var parent = Get(r, "ParentCategoryKey");
            return new ParsedCategoryRow(
                i + 1,
                slugs[i],
                Get(r, "NameEN"),
                CatalogImportParser.NullIfEmpty(Get(r, "NameAR")),
                parent.Length == 0 ? null : keyToSlug.GetValueOrDefault(parent, parent),
                status is null || status.Equals("Active", StringComparison.OrdinalIgnoreCase),
                CatalogImportParser.ParseInt(Get(r, "SortOrder"), 0),
                status,
                CatalogImportParser.NullIfEmpty(Get(r, "DescriptionEN")),
                CatalogImportParser.NullIfEmpty(Get(r, "DescriptionAR")),
                List(Get(r, "FilterGroups")),
                List(Get(r, "ExcludedFilterGroups")));
        }).ToList();

        return (categories, collections);
    }

    // ---------- Products (parents and variants in one sheet) ----------

    public static (List<ParsedProductRow> Products, List<ParsedOptionGroupRow> Groups, List<ParsedOptionRow> Options, List<ParsedVariantRow> Variants)
        Products(List<Dictionary<string, string>> rows)
    {
        static bool IsVariant(Dictionary<string, string> r) =>
            Get(r, "RecordType").Equals("Variant", StringComparison.OrdinalIgnoreCase)
            || (Get(r, "RecordType").Length == 0 && Get(r, "ParentSKU").Length > 0);

        var products = new List<ParsedProductRow>();
        var groups = new List<ParsedOptionGroupRow>();
        var options = new List<ParsedOptionRow>();
        var variants = new List<ParsedVariantRow>();
        var parentInfo = new Dictionary<string, (string Name, List<string> Groups)>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (IsVariant(r))
            {
                continue;
            }

            var name = Get(r, "NameEN");
            var sku = Get(r, "SKU");
            var key = sku.Length > 0 ? sku : name;
            var groupNames = List(Get(r, "VariantGroups"));
            parentInfo[key] = (name, groupNames);
            products.Add(new ParsedProductRow(
                i + 1, key, name,
                CatalogImportParser.NullIfEmpty(Get(r, "NameAR")),
                CatalogImportParser.NullIfEmpty(Get(r, "DescriptionEN")),
                CatalogImportParser.NullIfEmpty(Get(r, "DescriptionAR")),
                null, Get(r, "PrimaryCategory"), CatalogImportParser.NullIfEmpty(Get(r, "Status")), 0, [],
                List(Get(r, "AdditionalCategories")), List(Get(r, "InternalCategories")),
                CatalogImportParser.NullIfEmpty(Get(r, "AdditionalDescriptionEN")),
                CatalogImportParser.NullIfEmpty(Get(r, "AdditionalDescriptionAR")),
                CatalogImportParser.NullIfEmpty(Get(r, "MainImageRef"))));
            for (var g = 0; g < groupNames.Count; g++)
            {
                groups.Add(new ParsedOptionGroupRow(key, groupNames[g], groupNames[g], null, g, true, groups.Count + 1));
            }
        }

        var seenOptions = new HashSet<(string, string, string)>();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (!IsVariant(r))
            {
                continue;
            }

            var parentKey = Get(r, "ParentSKU");
            parentInfo.TryGetValue(parentKey, out var parent);
            var chosen = VariantOptions(r, parent.Name ?? string.Empty, parent.Groups ?? []);
            foreach (var (group, value) in chosen)
            {
                if (seenOptions.Add((parentKey.ToUpperInvariant(), group.ToUpperInvariant(), value.ToUpperInvariant())))
                {
                    options.Add(new ParsedOptionRow(parentKey, group, value, value, options.Count, options.Count + 1));
                }
            }

            variants.Add(new ParsedVariantRow(
                i + 1,
                CatalogImportParser.NullIfEmpty(Get(r, "SKU")),
                parentKey,
                null, null,
                CatalogImportParser.NullIfEmpty(Get(r, "Status")),
                5, null,
                chosen.Select(c => c.Value).ToList(),
                CatalogImportParser.NullIfEmpty(Get(r, "AdditionalDescriptionEN"))));
        }

        return (products, groups, options, variants);
    }

    /// <summary>
    /// The option a variant row picks in each of its parent's groups. The optional <c>VariantOptions</c> column
    /// ("Duration=1 Month; Region=US", written by our own export) wins; otherwise the values are read from the end of the
    /// variant's name ("Parent - 1 Month - US"), which only works when there is exactly one value per group.
    /// </summary>
    private static List<(string Group, string Value)> VariantOptions(Dictionary<string, string> row, string parentName, List<string> groupNames)
    {
        var explicitOptions = Get(row, "VariantOptions");
        if (explicitOptions.Length > 0)
        {
            return explicitOptions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(pair => pair.Split('=', 2, StringSplitOptions.TrimEntries))
                .Where(p => p.Length == 2 && p[0].Length > 0 && p[1].Length > 0)
                .Select(p => (p[0], p[1]))
                .ToList();
        }

        var name = Get(row, "NameEN");
        var suffix = parentName.Length > 0 && name.StartsWith(parentName + " - ", StringComparison.OrdinalIgnoreCase)
            ? name[(parentName.Length + 3)..]
            : name.Contains(" - ") ? name[(name.IndexOf(" - ", StringComparison.Ordinal) + 3)..] : string.Empty;
        var values = suffix.Split(" - ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return values.Length == groupNames.Count && values.Length > 0
            ? groupNames.Zip(values, (g, v) => (g, v)).ToList()
            : [];
    }

    // ---------- Pricing ----------

    public static List<ParsedPriceRow> Pricing(List<Dictionary<string, string>> rows) =>
        rows.Select((r, i) => (r, i))
            .Where(x => Get(x.r, "SKU").Length > 0)
            .Select(x =>
            {
                var r = x.r;
                var error = new List<string>();

                decimal? Amount(params string[] keys)
                {
                    var text = Get(r, keys);
                    if (text.Length == 0)
                    {
                        return null;
                    }

                    if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                    {
                        return value;
                    }

                    error.Add($"'{text}' is not a number.");
                    return null;
                }

                DateTimeOffset? When(string key)
                {
                    var text = Get(r, key);
                    if (text.Length == 0)
                    {
                        return null;
                    }

                    if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
                    {
                        return value;
                    }

                    error.Add($"{key} '{text}' is not a date.");
                    return null;
                }

                var currency = Get(r, "Currency").ToUpperInvariant();
                return new ParsedPriceRow(
                    x.i + 1, Get(r, "SKU"), currency.Length == 0 ? "USD" : currency,
                    Amount("Cost Price", "CostPrice"), Amount("Normal Price", "NormalPrice"), Amount("Sale Price", "SalePrice"),
                    Amount("Elite Price", "ElitePrice"), When("Sale Start"), EndOfDay(When("Sale End")),
                    error.Count == 0 ? null : string.Join(' ', error));
            })
            .ToList();

    /// <summary>A Sale End typed as a day means the whole day: the sale runs to its last second.</summary>
    private static DateTimeOffset? EndOfDay(DateTimeOffset? end) =>
        end is { } value && value.TimeOfDay == TimeSpan.Zero ? value.AddDays(1).AddSeconds(-1) : end;

    // ---------- Stock ----------

    public static List<ParsedCodeRow> Stock(List<Dictionary<string, string>> rows)
    {
        var codes = new List<ParsedCodeRow>();
        foreach (var r in rows)
        {
            var sku = Get(r, "SKU");
            var data = Get(r, "StockData");
            if (sku.Length == 0 && data.Length == 0)
            {
                continue;
            }

            var note = CatalogImportParser.NullIfEmpty(Get(r, "InternalNote"));
            var lines = Get(r, "EntryMode").Equals("WholeEntry", StringComparison.OrdinalIgnoreCase)
                ? [data.Trim()]
                : data.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                codes.Add(new ParsedCodeRow(codes.Count + 1, sku, line, null, null, null, "USD", null, null, note));
            }
        }

        return codes;
    }
}
