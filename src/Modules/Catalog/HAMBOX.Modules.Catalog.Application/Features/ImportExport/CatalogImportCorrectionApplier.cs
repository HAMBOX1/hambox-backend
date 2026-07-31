namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport;

/// <summary>
/// Applies "Apply to all N occurrences"-style bulk corrections to an already-parsed package, before
/// <see cref="CatalogImportMatcher.BuildPlanAsync"/> runs — this is what lets the wizard revalidate
/// after an inline fix without re-uploading the file. Deliberately a fixed, hand-written switch over
/// the small set of correctable (EntityType, Column) pairs that <see cref="CatalogImportMatcher"/>
/// actually reports <see cref="CatalogImportFieldIssue"/>s for, matching the codebase's
/// hand-written-mapper convention rather than reflection.
/// </summary>
public static class CatalogImportCorrectionApplier
{
    public static ParsedCatalogPackage Apply(ParsedCatalogPackage package, IReadOnlyList<CatalogImportCorrection>? corrections)
    {
        if (corrections is null || corrections.Count == 0)
        {
            return package;
        }

        var categories = package.Categories;
        var collections = package.Collections;
        var products = package.Products;
        var optionGroups = package.OptionGroups;
        var options = package.Options;
        var variants = package.Variants;
        var codes = package.Codes;
        var supplierMappings = package.SupplierMappings;

        foreach (var correction in corrections)
        {
            switch (correction.EntityType, correction.Column)
            {
                case ("Category", "ParentSlug"):
                    categories = categories.Select(r => Matches(r.ParentSlug, correction)
                        ? r with { ParentSlug = correction.ToValue } : r).ToList();
                    categories = EnsureCategoryCreated(categories, correction);
                    break;

                case ("Collection", "ParentName"):
                    collections = collections.Select(r => Matches(r.ParentName, correction)
                        ? r with { ParentName = correction.ToValue } : r).ToList();
                    collections = EnsureCollectionCreated(collections, correction);
                    break;

                case ("Product", "CategorySlug"):
                    products = products.Select(r => Matches(r.CategorySlug, correction)
                        ? r with { CategorySlug = correction.ToValue } : r).ToList();
                    categories = EnsureCategoryCreated(categories, correction);
                    break;

                case ("Product", "AdditionalCategorySlugs"):
                    products = products.Select(r => r with { AdditionalCategorySlugs = ReplaceInList(r.AdditionalCategorySlugs, correction) }).ToList();
                    categories = EnsureCategoryCreated(categories, correction);
                    break;

                case ("Product", "CollectionNames"):
                    products = products.Select(r => r with { CollectionNames = ReplaceInList(r.CollectionNames ?? [], correction) }).ToList();
                    collections = EnsureCollectionCreated(collections, correction);
                    break;

                case ("VariantGroup", "ProductImportKey"):
                    optionGroups = optionGroups.Select(r => Matches(r.ProductImportKey, correction)
                        ? r with { ProductImportKey = correction.ToValue } : r).ToList();
                    break;

                case ("VariantGroup", "ParentKey"):
                    optionGroups = optionGroups.Select(r => Matches(r.ParentKey, correction)
                        ? r with { ParentKey = correction.ToValue } : r).ToList();
                    break;

                case ("VariantOption", "ProductImportKey"):
                    options = options.Select(r => Matches(r.ProductImportKey, correction)
                        ? r with { ProductImportKey = correction.ToValue } : r).ToList();
                    break;

                case ("VariantOption", "GroupKey"):
                    options = options.Select(r => Matches(r.GroupKey, correction)
                        ? r with { GroupKey = correction.ToValue } : r).ToList();
                    break;

                case ("Variant", "ProductImportKey"):
                    variants = variants.Select(r => Matches(r.ProductImportKey, correction)
                        ? r with { ProductImportKey = correction.ToValue } : r).ToList();
                    break;

                case ("Variant", "SelectedOptionValues"):
                    variants = variants.Select(r => r with { SelectedOptionValues = ReplaceInList(r.SelectedOptionValues, correction) }).ToList();
                    break;

                case ("Variant", "Sku"):
                    variants = variants.Select(r => Matches(r.Sku, correction)
                        ? r with { Sku = correction.ToValue } : r).ToList();
                    break;

                case ("Code", "VariantSku"):
                    codes = codes.Select(r => Matches(r.VariantSku, correction)
                        ? r with { VariantSku = correction.ToValue } : r).ToList();
                    break;

                case ("SupplierMapping", "ProductImportKey"):
                    supplierMappings = supplierMappings.Select(r => Matches(r.ProductImportKey, correction)
                        ? r with { ProductImportKey = correction.ToValue } : r).ToList();
                    break;

                case ("SupplierMapping", "SupplierCode"):
                    supplierMappings = supplierMappings.Select(r => Matches(r.SupplierCode, correction)
                        ? r with { SupplierCode = correction.ToValue } : r).ToList();
                    break;
            }
        }

        return package with
        {
            Categories = categories,
            Collections = collections,
            Products = products,
            OptionGroups = optionGroups,
            Options = options,
            Variants = variants,
            Codes = codes,
            SupplierMappings = supplierMappings,
        };
    }

    private static bool Matches(string? value, CatalogImportCorrection correction) =>
        string.Equals(value, correction.FromValue, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ReplaceInList(IReadOnlyList<string> values, CatalogImportCorrection correction) =>
        values.Select(v => Matches(v, correction) ? correction.ToValue : v).ToList();

    /// <summary>"Create category" correction: appends a new Categories row (NameEn = ToValue) if one doesn't already exist by name or slug. A no-op unless <see cref="CatalogImportCorrection.CreateNew"/> is set.</summary>
    private static IReadOnlyList<ParsedCategoryRow> EnsureCategoryCreated(IReadOnlyList<ParsedCategoryRow> categories, CatalogImportCorrection correction)
    {
        if (!correction.CreateNew || string.IsNullOrWhiteSpace(correction.ToValue))
        {
            return categories;
        }

        if (categories.Any(c => string.Equals(c.NameEn, correction.ToValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Slug, correction.ToValue, StringComparison.OrdinalIgnoreCase)))
        {
            return categories;
        }

        return [.. categories, new ParsedCategoryRow(0, Slugify(correction.ToValue), correction.ToValue, null, null, true, 0)];
    }

    /// <summary>"Create collection" correction — same idea as <see cref="EnsureCategoryCreated"/>, but collections dedupe on Name alone (no slug).</summary>
    private static IReadOnlyList<ParsedCollectionRow> EnsureCollectionCreated(IReadOnlyList<ParsedCollectionRow> collections, CatalogImportCorrection correction)
    {
        if (!correction.CreateNew || string.IsNullOrWhiteSpace(correction.ToValue))
        {
            return collections;
        }

        if (collections.Any(c => string.Equals(c.Name, correction.ToValue, StringComparison.OrdinalIgnoreCase)))
        {
            return collections;
        }

        return [.. collections, new ParsedCollectionRow(0, correction.ToValue, null, null, null, null, 0)];
    }

    private static string Slugify(string value)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 0 ? slug : Guid.NewGuid().ToString("N")[..8];
    }
}
