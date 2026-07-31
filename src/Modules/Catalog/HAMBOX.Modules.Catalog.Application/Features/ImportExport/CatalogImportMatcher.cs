using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport;

/// <summary>One parsed row plus the outcome of matching it against the destination database.</summary>
public sealed record CatalogImportPlanRow<TRow>(
    TRow Row,
    CatalogImportRowStatus Status,
    Guid? ExistingId,
    IReadOnlyList<string> Errors,
    IReadOnlyList<CatalogImportFieldIssue> FieldIssues);

/// <summary>
/// The full New/Updated/Duplicate/Invalid decision for every row in a package, computed once and
/// shared by both <c>ValidateCatalogImportQueryHandler</c> (read-only report) and the Execute job
/// handler (which applies the chosen <see cref="CatalogDuplicateStrategy"/> on top of this same
/// plan) — so what Validate shows the admin is exactly what Execute acts on.
/// </summary>
public sealed record CatalogImportPlan(
    IReadOnlyList<CatalogImportPlanRow<ParsedCategoryRow>> Categories,
    IReadOnlyList<CatalogImportPlanRow<ParsedCollectionRow>> Collections,
    IReadOnlyList<CatalogImportPlanRow<ParsedProductRow>> Products,
    IReadOnlyList<CatalogImportPlanRow<ParsedVariantRow>> Variants,
    IReadOnlyList<CatalogImportPlanRow<ParsedCodeRow>> Codes,
    IReadOnlyList<CatalogImportPlanRow<ParsedOptionGroupRow>> OptionGroups,
    IReadOnlyList<CatalogImportPlanRow<ParsedOptionRow>> Options,
    IReadOnlyList<CatalogImportPlanRow<ParsedSupplierMappingRow>> SupplierMappings,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Duplicate-detection keys: Category by <c>Slug</c>, Product by case-insensitive
/// (<c>CategorySlug</c>, <c>NameEn</c>), Variant by <c>Sku</c>, Code by <c>CodeHash</c> — see
/// <see cref="CatalogDedupeKeys"/>. "Updated" vs. "Duplicate" is decided by comparing the row's
/// other fields against the existing record: identical → Duplicate (a true no-op), any difference
/// → Updated (this row would change something once a strategy other than Skip is applied).
/// </summary>
public static class CatalogImportMatcher
{
    public static async Task<CatalogImportPlan> BuildPlanAsync(
        ParsedCatalogPackage package, ICatalogDbContext db, CatalogSkuStrategy skuStrategy, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        // ---------- Categories ----------
        var existingCategories = await db.Categories.AsNoTracking()
            .Select(c => new { c.Id, c.Slug, c.NameEn, c.NameAr, c.IsActive, c.SortOrder })
            .ToListAsync(cancellationToken);
        var existingCategoryBySlug = existingCategories
            .ToDictionary(c => c.Slug, StringComparer.OrdinalIgnoreCase);
        var packageCategorySlugs = package.Categories
            .Select(c => c.Slug)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool CategoryExists(string slug) => existingCategoryBySlug.ContainsKey(slug) || packageCategorySlugs.Contains(slug);

        var categoryRows = new List<CatalogImportPlanRow<ParsedCategoryRow>>();
        foreach (var row in package.Categories)
        {
            var errors = new List<string>();
            var fieldIssues = new List<CatalogImportFieldIssue>();
            if (string.IsNullOrWhiteSpace(row.Slug))
            {
                errors.Add("Slug is required.");
            }

            if (string.IsNullOrWhiteSpace(row.NameEn))
            {
                errors.Add("NameEn is required.");
            }

            if (!string.IsNullOrWhiteSpace(row.ParentSlug) && !CategoryExists(row.ParentSlug))
            {
                errors.Add($"Parent category '{row.ParentSlug}' was not found.");
                fieldIssues.Add(new("ParentSlug", row.ParentSlug, "UnknownCategory"));
            }

            if (errors.Count > 0)
            {
                categoryRows.Add(new(row, CatalogImportRowStatus.Invalid, null, errors, fieldIssues));
                continue;
            }

            if (existingCategoryBySlug.TryGetValue(row.Slug, out var existing))
            {
                var changed = !string.Equals(existing.NameEn, row.NameEn, StringComparison.Ordinal)
                    || !string.Equals(existing.NameAr, row.NameAr ?? row.NameEn, StringComparison.Ordinal)
                    || existing.IsActive != row.IsActive
                    || existing.SortOrder != row.SortOrder;
                categoryRows.Add(new(row, changed ? CatalogImportRowStatus.Updated : CatalogImportRowStatus.Duplicate, existing.Id, [], []));
            }
            else
            {
                categoryRows.Add(new(row, CatalogImportRowStatus.New, null, [], []));
            }
        }

        // ---------- Collections (dedupe by Name+ParentName since collections have no slug) ----------
        var existingCollections = await db.ProductCollections.AsNoTracking()
            .Select(c => new { c.Id, c.Name, c.Description, c.Color, c.Icon, c.ParentId, c.SortOrder })
            .ToListAsync(cancellationToken);
        var collectionNameById = existingCollections.ToDictionary(c => c.Id, c => c.Name);
        var existingCollectionByKey = existingCollections
            .GroupBy(c => (Name: c.Name, ParentName: c.ParentId.HasValue ? collectionNameById.GetValueOrDefault(c.ParentId.Value) : null), new CollectionKeyComparer())
            .ToDictionary(g => g.Key, g => g.First(), new CollectionKeyComparer());
        var packageCollectionNames = package.Collections
            .Select(c => c.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool CollectionExists(string name) =>
            existingCollectionByKey.Keys.Any(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase))
            || packageCollectionNames.Contains(name);

        var collectionRows = new List<CatalogImportPlanRow<ParsedCollectionRow>>();
        foreach (var row in package.Collections)
        {
            var errors = new List<string>();
            var fieldIssues = new List<CatalogImportFieldIssue>();
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                errors.Add("Name is required.");
            }

            if (!string.IsNullOrWhiteSpace(row.ParentName) && !CollectionExists(row.ParentName))
            {
                errors.Add($"Parent collection '{row.ParentName}' was not found.");
                fieldIssues.Add(new("ParentName", row.ParentName, "UnknownCollection"));
            }

            if (errors.Count > 0)
            {
                collectionRows.Add(new(row, CatalogImportRowStatus.Invalid, null, errors, fieldIssues));
                continue;
            }

            if (existingCollectionByKey.TryGetValue((row.Name, row.ParentName), out var existing))
            {
                var changed = !string.Equals(existing.Description ?? string.Empty, row.Description ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(existing.Color ?? string.Empty, row.Color ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(existing.Icon ?? string.Empty, row.Icon ?? string.Empty, StringComparison.Ordinal)
                    || existing.SortOrder != row.SortOrder;
                collectionRows.Add(new(row, changed ? CatalogImportRowStatus.Updated : CatalogImportRowStatus.Duplicate, existing.Id, [], []));
            }
            else
            {
                collectionRows.Add(new(row, CatalogImportRowStatus.New, null, [], []));
            }
        }

        // ---------- Products ----------
        var existingProducts = await db.Products.AsNoTracking()
            .Select(p => new
            {
                p.Id, p.NameEn, p.NameAr, p.DescriptionEn, p.DescriptionAr, p.Price, p.Status,
                p.StockQuantity, p.CategoryId,
            })
            .ToListAsync(cancellationToken);
        var categorySlugById = existingCategories.ToDictionary(c => c.Id, c => c.Slug);

        // Nothing in the schema stops two products sharing a (category, name) pair — dedupe by
        // taking the first match rather than assuming uniqueness a naive ToDictionary would crash on.
        var existingProductByKey = existingProducts
            .Where(p => categorySlugById.ContainsKey(p.CategoryId))
            .GroupBy(p => (Slug: categorySlugById[p.CategoryId], p.NameEn), new ProductKeyComparer())
            .ToDictionary(g => g.Key, g => g.First(), new ProductKeyComparer());

        var productRows = new List<CatalogImportPlanRow<ParsedProductRow>>();
        var productIdByImportKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in package.Products)
        {
            var errors = new List<string>();
            var fieldIssues = new List<CatalogImportFieldIssue>();
            if (string.IsNullOrWhiteSpace(row.NameEn))
            {
                errors.Add("NameEn is required.");
            }

            if (row.Price < 0)
            {
                errors.Add("Price must not be negative.");
            }

            if (row.StockQuantity < 0)
            {
                errors.Add("StockQuantity must not be negative.");
            }

            if (!CategoryExists(row.CategorySlug))
            {
                errors.Add($"Category '{row.CategorySlug}' was not found.");
                fieldIssues.Add(new("CategorySlug", row.CategorySlug, "UnknownCategory"));
            }

            foreach (var additionalSlug in row.AdditionalCategorySlugs)
            {
                if (!CategoryExists(additionalSlug))
                {
                    errors.Add($"Additional category '{additionalSlug}' was not found.");
                    fieldIssues.Add(new("AdditionalCategorySlugs", additionalSlug, "UnknownCategory"));
                }
            }

            foreach (var collectionName in row.CollectionNames ?? [])
            {
                if (!CollectionExists(collectionName))
                {
                    errors.Add($"Collection '{collectionName}' was not found.");
                    fieldIssues.Add(new("CollectionNames", collectionName, "UnknownCollection"));
                }
            }

            if (errors.Count > 0)
            {
                productRows.Add(new(row, CatalogImportRowStatus.Invalid, null, errors, fieldIssues));
                continue;
            }

            if (existingProductByKey.TryGetValue((row.CategorySlug, row.NameEn), out var existing))
            {
                if (!string.IsNullOrWhiteSpace(row.ImportKey))
                {
                    productIdByImportKey[row.ImportKey] = existing.Id;
                }

                var changed = existing.Price != row.Price
                    || existing.StockQuantity != row.StockQuantity
                    || !string.Equals(existing.DescriptionEn, row.DescriptionEn ?? existing.DescriptionEn, StringComparison.Ordinal)
                    || (row.Status is not null && existing.Status.ToString() != row.Status);
                productRows.Add(new(row, changed ? CatalogImportRowStatus.Updated : CatalogImportRowStatus.Duplicate, existing.Id, [], []));
            }
            else
            {
                productRows.Add(new(row, CatalogImportRowStatus.New, null, [], []));
            }
        }

        bool ProductResolvable(string importKey) =>
            package.Products.Any(p => string.Equals(p.ImportKey, importKey, StringComparison.OrdinalIgnoreCase))
            || existingProductByKey.Values.Any(p => p.Id.ToString().Equals(importKey, StringComparison.OrdinalIgnoreCase))
            || existingProducts.Any(p => string.Equals(p.NameEn, importKey, StringComparison.OrdinalIgnoreCase));

        // ---------- Variant option groups / options (resolved before Variants, since a variant's
        // SelectedOptionValues must be checked against them) ----------
        var existingOptionGroups = await db.ProductOptionGroups.AsNoTracking()
            .Select(g => new { g.Id, g.ProductId, g.Key })
            .ToListAsync(cancellationToken);
        var existingOptionGroupKeysByProduct = existingOptionGroups
            .GroupBy(g => g.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase));

        bool OptionGroupResolvable(string productImportKey, string groupKey)
        {
            if (package.OptionGroups.Any(g =>
                    string.Equals(g.ProductImportKey, productImportKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(g.Key, groupKey, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var existingProductId = existingProducts.FirstOrDefault(p => string.Equals(p.NameEn, productImportKey, StringComparison.OrdinalIgnoreCase))?.Id
                ?? existingProductByKey.Values.FirstOrDefault(p => p.Id.ToString().Equals(productImportKey, StringComparison.OrdinalIgnoreCase))?.Id;

            return existingProductId is { } id
                && existingOptionGroupKeysByProduct.TryGetValue(id, out var keys)
                && keys.Contains(groupKey);
        }

        var optionGroupRows = new List<CatalogImportPlanRow<ParsedOptionGroupRow>>();
        foreach (var row in package.OptionGroups)
        {
            var errors = new List<string>();
            var fieldIssues = new List<CatalogImportFieldIssue>();

            if (!ProductResolvable(row.ProductImportKey))
            {
                errors.Add($"Product '{row.ProductImportKey}' was not found.");
                fieldIssues.Add(new("ProductImportKey", row.ProductImportKey, "UnknownProduct"));
            }

            if (!string.IsNullOrWhiteSpace(row.ParentKey) && !OptionGroupResolvable(row.ProductImportKey, row.ParentKey))
            {
                errors.Add($"Parent variant group '{row.ParentKey}' was not found.");
                fieldIssues.Add(new("ParentKey", row.ParentKey, "UnknownVariantGroup"));
            }

            optionGroupRows.Add(errors.Count > 0
                ? new(row, CatalogImportRowStatus.Invalid, null, errors, fieldIssues)
                : new(row, CatalogImportRowStatus.New, null, [], []));
        }

        var packageOptionGroupKeys = package.OptionGroups
            .Select(g => (g.ProductImportKey, g.Key))
            .ToHashSet(new ProductScopedKeyComparer());

        var optionRows = new List<CatalogImportPlanRow<ParsedOptionRow>>();
        foreach (var row in package.Options)
        {
            var errors = new List<string>();
            var fieldIssues = new List<CatalogImportFieldIssue>();

            if (!ProductResolvable(row.ProductImportKey))
            {
                errors.Add($"Product '{row.ProductImportKey}' was not found.");
                fieldIssues.Add(new("ProductImportKey", row.ProductImportKey, "UnknownProduct"));
            }
            else if (!OptionGroupResolvable(row.ProductImportKey, row.GroupKey))
            {
                errors.Add($"Variant group '{row.GroupKey}' was not found.");
                fieldIssues.Add(new("GroupKey", row.GroupKey, "UnknownVariantGroup"));
            }

            optionRows.Add(errors.Count > 0
                ? new(row, CatalogImportRowStatus.Invalid, null, errors, fieldIssues)
                : new(row, CatalogImportRowStatus.New, null, [], []));
        }

        var packageOptionValues = package.Options
            .Select(o => (o.ProductImportKey, Value: o.Value))
            .ToHashSet(new ProductScopedKeyComparer());

        bool OptionValueResolvable(string productImportKey, string value) =>
            packageOptionValues.Contains((productImportKey, value));

        // ---------- Variants ----------
        var existingVariants = await db.ProductVariants.AsNoTracking()
            .Select(v => new { v.Id, v.Sku, v.PriceOverride, v.ComparePrice, v.Status, v.LowStockThreshold, v.ProductId })
            .ToListAsync(cancellationToken);
        var existingVariantBySku = existingVariants.ToDictionary(v => v.Sku, StringComparer.OrdinalIgnoreCase);

        var variantRows = new List<CatalogImportPlanRow<ParsedVariantRow>>();
        foreach (var row in package.Variants)
        {
            var errors = new List<string>();
            var fieldIssues = new List<CatalogImportFieldIssue>();

            if (string.IsNullOrWhiteSpace(row.Sku) && skuStrategy == CatalogSkuStrategy.UseImportedSku)
            {
                errors.Add("Sku is required.");
            }

            if (!ProductResolvable(row.ProductImportKey))
            {
                errors.Add($"Product '{row.ProductImportKey}' was not found.");
                fieldIssues.Add(new("ProductImportKey", row.ProductImportKey, "UnknownProduct"));
            }

            foreach (var optionValue in row.SelectedOptionValues)
            {
                if (!OptionValueResolvable(row.ProductImportKey, optionValue))
                {
                    errors.Add($"Variant option '{optionValue}' was not found.");
                    fieldIssues.Add(new("SelectedOptionValues", optionValue, "UnknownVariantOption"));
                }
            }

            if (row.PriceOverride is < 0)
            {
                errors.Add("PriceOverride must not be negative.");
            }

            if (row.ComparePrice is < 0)
            {
                errors.Add("ComparePrice must not be negative.");
            }

            if (row.LowStockThreshold < 0)
            {
                errors.Add("LowStockThreshold must not be negative.");
            }

            if (errors.Count > 0)
            {
                variantRows.Add(new(row, CatalogImportRowStatus.Invalid, null, errors, fieldIssues));
                continue;
            }

            if (row.MembershipPlanId is not null)
            {
                warnings.Add($"Variant '{row.Sku}': membership plan link was not migrated — reassign it manually.");
            }

            // AutoGenerate discards whatever Sku the file supplies, so a provided value can never
            // collide with an existing variant — it's always New from the matcher's point of view.
            if (skuStrategy != CatalogSkuStrategy.AutoGenerate
                && !string.IsNullOrWhiteSpace(row.Sku) && existingVariantBySku.TryGetValue(row.Sku, out var existing))
            {
                var changed = existing.PriceOverride != row.PriceOverride
                    || existing.ComparePrice != row.ComparePrice
                    || existing.LowStockThreshold != row.LowStockThreshold
                    || (row.Status is not null && existing.Status.ToString() != row.Status);
                var status = changed ? CatalogImportRowStatus.Updated : CatalogImportRowStatus.Duplicate;
                variantRows.Add(new(row, status, existing.Id, [], [new CatalogImportFieldIssue("Sku", row.Sku, "DuplicateSku")]));
            }
            else
            {
                variantRows.Add(new(row, CatalogImportRowStatus.New, null, [], []));
            }
        }

        var packageVariantSkus = package.Variants
            .Where(v => !string.IsNullOrWhiteSpace(v.Sku))
            .Select(v => v.Sku!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ---------- Digital codes ----------
        var existingCodeHashes = await db.DigitalInventoryCodes.AsNoTracking()
            .Select(c => c.CodeHash)
            .ToListAsync(cancellationToken);
        var existingCodeHashSet = existingCodeHashes.ToHashSet(StringComparer.Ordinal);

        var codeRows = new List<CatalogImportPlanRow<ParsedCodeRow>>();
        foreach (var row in package.Codes)
        {
            var errors = new List<string>();
            var fieldIssues = new List<CatalogImportFieldIssue>();
            if (string.IsNullOrWhiteSpace(row.Code))
            {
                errors.Add("Code is required.");
            }

            if (!packageVariantSkus.Contains(row.VariantSku) && !existingVariantBySku.ContainsKey(row.VariantSku))
            {
                errors.Add($"Variant '{row.VariantSku}' was not found.");
                fieldIssues.Add(new("VariantSku", row.VariantSku, "UnknownVariant"));
            }

            if (row.Currency.Length != 3)
            {
                errors.Add("Currency must be a 3-letter code.");
            }

            if (row.PurchaseCost is < 0)
            {
                errors.Add("PurchaseCost must not be negative.");
            }

            if (errors.Count > 0)
            {
                codeRows.Add(new(row, CatalogImportRowStatus.Invalid, null, errors, fieldIssues));
                continue;
            }

            var hash = DigitalInventoryCode.ComputeHash(row.Code);
            codeRows.Add(existingCodeHashSet.Contains(hash)
                ? new(row, CatalogImportRowStatus.Duplicate, null, [], [])
                : new(row, CatalogImportRowStatus.New, null, [], []));
        }

        // ---------- Supplier mappings: lighter-touch pass-through ----------
        var supplierMappingRows = package.SupplierMappings.Select(row =>
            ProductResolvable(row.ProductImportKey)
                ? new CatalogImportPlanRow<ParsedSupplierMappingRow>(row, CatalogImportRowStatus.New, null, [], [])
                : new CatalogImportPlanRow<ParsedSupplierMappingRow>(
                    row, CatalogImportRowStatus.Invalid, null,
                    [$"Product '{row.ProductImportKey}' was not found."],
                    [new CatalogImportFieldIssue("ProductImportKey", row.ProductImportKey, "UnknownProduct")]))
            .ToList();

        return new CatalogImportPlan(
            categoryRows, collectionRows, productRows, variantRows, codeRows, optionGroupRows, optionRows, supplierMappingRows, warnings);
    }

    public static CatalogImportValidationReport ToReport(
        Guid uploadId, Domain.Enums.CatalogPackageFormat format, CatalogImportEntityType entityType, CatalogImportPlan plan)
    {
        var rows = new List<CatalogImportRowResult>();
        rows.AddRange(plan.Categories.Select((r, i) => ToRowResult(i + 1, "Category", r.Row.Slug, r)));
        rows.AddRange(plan.Collections.Select((r, i) => ToRowResult(i + 1, "Collection", r.Row.Name, r)));
        rows.AddRange(plan.Products.Select((r, i) => ToRowResult(i + 1, "Product", r.Row.NameEn, r)));
        rows.AddRange(plan.OptionGroups.Select((r, i) => ToRowResult(i + 1, "VariantGroup", r.Row.Key, r)));
        rows.AddRange(plan.Options.Select((r, i) => ToRowResult(i + 1, "VariantOption", r.Row.Value, r)));
        rows.AddRange(plan.Variants.Select((r, i) => ToRowResult(i + 1, "Variant", r.Row.Sku ?? "(auto)", r)));
        rows.AddRange(plan.Codes.Select((r, i) => ToRowResult(i + 1, "Code", MaskCode(r.Row.Code), r)));
        rows.AddRange(plan.SupplierMappings.Select((r, i) => ToRowResult(i + 1, "SupplierMapping", r.Row.ExternalProductId, r)));

        return new CatalogImportValidationReport(
            uploadId,
            format,
            entityType,
            rows.Count(r => r.Status == CatalogImportRowStatus.New),
            rows.Count(r => r.Status == CatalogImportRowStatus.Updated),
            rows.Count(r => r.Status == CatalogImportRowStatus.Duplicate),
            rows.Count(r => r.Status == CatalogImportRowStatus.Invalid),
            plan.Warnings,
            rows);
    }

    private static CatalogImportRowResult ToRowResult<TRow>(int rowNumber, string entityType, string label, CatalogImportPlanRow<TRow> planRow) =>
        new(rowNumber, entityType, label, planRow.Status, planRow.Errors, planRow.FieldIssues);

    private static string MaskCode(string code) =>
        code.Length <= 4 ? "****" : $"{code[..2]}****{code[^2..]}";

    private sealed class ProductKeyComparer : IEqualityComparer<(string Slug, string NameEn)>
    {
        public bool Equals((string Slug, string NameEn) x, (string Slug, string NameEn) y) =>
            string.Equals(x.Slug, y.Slug, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.NameEn, y.NameEn, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Slug, string NameEn) obj) =>
            HashCode.Combine(obj.Slug.ToUpperInvariant(), obj.NameEn.ToUpperInvariant());
    }

    private sealed class CollectionKeyComparer : IEqualityComparer<(string Name, string? ParentName)>
    {
        public bool Equals((string Name, string? ParentName) x, (string Name, string? ParentName) y) =>
            string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ParentName ?? string.Empty, y.ParentName ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Name, string? ParentName) obj) =>
            HashCode.Combine(obj.Name.ToUpperInvariant(), (obj.ParentName ?? string.Empty).ToUpperInvariant());
    }

    /// <summary>Matches a (ProductImportKey, Value) pair case-insensitively — used for both option-group-key and option-value scoping.</summary>
    private sealed class ProductScopedKeyComparer : IEqualityComparer<(string ProductImportKey, string Value)>
    {
        public bool Equals((string ProductImportKey, string Value) x, (string ProductImportKey, string Value) y) =>
            string.Equals(x.ProductImportKey, y.ProductImportKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string ProductImportKey, string Value) obj) =>
            HashCode.Combine(obj.ProductImportKey.ToUpperInvariant(), obj.Value.ToUpperInvariant());
    }
}
