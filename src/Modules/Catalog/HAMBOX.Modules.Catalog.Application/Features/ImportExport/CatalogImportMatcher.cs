using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport;

/// <summary>One parsed row plus the outcome of matching it against the destination database.</summary>
public sealed record CatalogImportPlanRow<TRow>(
    TRow Row, CatalogImportRowStatus Status, Guid? ExistingId, IReadOnlyList<string> Errors);

/// <summary>
/// The full New/Updated/Duplicate/Invalid decision for every row in a package, computed once and
/// shared by both <c>ValidateCatalogImportQueryHandler</c> (read-only report) and the Execute job
/// handler (which applies the chosen <see cref="CatalogDuplicateStrategy"/> on top of this same
/// plan) — so what Validate shows the admin is exactly what Execute acts on.
/// </summary>
public sealed record CatalogImportPlan(
    IReadOnlyList<CatalogImportPlanRow<ParsedCategoryRow>> Categories,
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
        ParsedCatalogPackage package, ICatalogDbContext db, CancellationToken cancellationToken)
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

        var categoryRows = new List<CatalogImportPlanRow<ParsedCategoryRow>>();
        foreach (var row in package.Categories)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(row.Slug))
            {
                errors.Add("Slug is required.");
            }

            if (string.IsNullOrWhiteSpace(row.NameEn))
            {
                errors.Add("NameEn is required.");
            }

            if (!string.IsNullOrWhiteSpace(row.ParentSlug)
                && !existingCategoryBySlug.ContainsKey(row.ParentSlug)
                && !packageCategorySlugs.Contains(row.ParentSlug))
            {
                errors.Add($"Parent category '{row.ParentSlug}' was not found.");
            }

            if (errors.Count > 0)
            {
                categoryRows.Add(new(row, CatalogImportRowStatus.Invalid, null, errors));
                continue;
            }

            if (existingCategoryBySlug.TryGetValue(row.Slug, out var existing))
            {
                var changed = !string.Equals(existing.NameEn, row.NameEn, StringComparison.Ordinal)
                    || !string.Equals(existing.NameAr, row.NameAr ?? row.NameEn, StringComparison.Ordinal)
                    || existing.IsActive != row.IsActive
                    || existing.SortOrder != row.SortOrder;
                categoryRows.Add(new(row, changed ? CatalogImportRowStatus.Updated : CatalogImportRowStatus.Duplicate, existing.Id, []));
            }
            else
            {
                categoryRows.Add(new(row, CatalogImportRowStatus.New, null, []));
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

            var categoryExists = existingCategoryBySlug.ContainsKey(row.CategorySlug) || packageCategorySlugs.Contains(row.CategorySlug);
            if (!categoryExists)
            {
                errors.Add($"Category '{row.CategorySlug}' was not found.");
            }

            if (errors.Count > 0)
            {
                productRows.Add(new(row, CatalogImportRowStatus.Invalid, null, errors));
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
                productRows.Add(new(row, changed ? CatalogImportRowStatus.Updated : CatalogImportRowStatus.Duplicate, existing.Id, []));
            }
            else
            {
                productRows.Add(new(row, CatalogImportRowStatus.New, null, []));
            }
        }

        // ---------- Variants ----------
        var existingVariants = await db.ProductVariants.AsNoTracking()
            .Select(v => new { v.Id, v.Sku, v.PriceOverride, v.ComparePrice, v.Status, v.LowStockThreshold, v.ProductId })
            .ToListAsync(cancellationToken);
        var existingVariantBySku = existingVariants.ToDictionary(v => v.Sku, StringComparer.OrdinalIgnoreCase);
        var packageProductKeys = package.Products.Select(p => p.ImportKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var variantRows = new List<CatalogImportPlanRow<ParsedVariantRow>>();
        foreach (var row in package.Variants)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(row.Sku))
            {
                errors.Add("Sku is required.");
            }

            var productResolvable = packageProductKeys.Contains(row.ProductImportKey)
                || existingProductByKey.Values.Any(p => p.Id.ToString().Equals(row.ProductImportKey, StringComparison.OrdinalIgnoreCase))
                || existingProducts.Any(p => string.Equals(p.NameEn, row.ProductImportKey, StringComparison.OrdinalIgnoreCase));
            if (!productResolvable)
            {
                errors.Add($"Product '{row.ProductImportKey}' was not found.");
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
                variantRows.Add(new(row, CatalogImportRowStatus.Invalid, null, errors));
                continue;
            }

            if (row.MembershipPlanId is not null)
            {
                warnings.Add($"Variant '{row.Sku}': membership plan link was not migrated — reassign it manually.");
            }

            if (existingVariantBySku.TryGetValue(row.Sku, out var existing))
            {
                var changed = existing.PriceOverride != row.PriceOverride
                    || existing.ComparePrice != row.ComparePrice
                    || existing.LowStockThreshold != row.LowStockThreshold
                    || (row.Status is not null && existing.Status.ToString() != row.Status);
                variantRows.Add(new(row, changed ? CatalogImportRowStatus.Updated : CatalogImportRowStatus.Duplicate, existing.Id, []));
            }
            else
            {
                variantRows.Add(new(row, CatalogImportRowStatus.New, null, []));
            }
        }

        // ---------- Digital codes ----------
        var existingCodeHashes = await db.DigitalInventoryCodes.AsNoTracking()
            .Select(c => c.CodeHash)
            .ToListAsync(cancellationToken);
        var existingCodeHashSet = existingCodeHashes.ToHashSet(StringComparer.Ordinal);
        var packageVariantSkus = package.Variants.Select(v => v.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var codeRows = new List<CatalogImportPlanRow<ParsedCodeRow>>();
        foreach (var row in package.Codes)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(row.Code))
            {
                errors.Add("Code is required.");
            }

            if (!packageVariantSkus.Contains(row.VariantSku) && !existingVariantBySku.ContainsKey(row.VariantSku))
            {
                errors.Add($"Variant '{row.VariantSku}' was not found.");
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
                codeRows.Add(new(row, CatalogImportRowStatus.Invalid, null, errors));
                continue;
            }

            var hash = DigitalInventoryCode.ComputeHash(row.Code);
            codeRows.Add(existingCodeHashSet.Contains(hash)
                ? new(row, CatalogImportRowStatus.Duplicate, null, [])
                : new(row, CatalogImportRowStatus.New, null, []));
        }

        // ---------- Option groups / options / supplier mappings: lighter-touch pass-through ----------
        var optionGroupRows = package.OptionGroups.Select(row =>
        {
            var resolvable = packageProductKeys.Contains(row.ProductImportKey)
                || existingProducts.Any(p => string.Equals(p.NameEn, row.ProductImportKey, StringComparison.OrdinalIgnoreCase));
            return resolvable
                ? new CatalogImportPlanRow<ParsedOptionGroupRow>(row, CatalogImportRowStatus.New, null, [])
                : new CatalogImportPlanRow<ParsedOptionGroupRow>(row, CatalogImportRowStatus.Invalid, null, [$"Product '{row.ProductImportKey}' was not found."]);
        }).ToList();

        var optionRows = package.Options.Select(row =>
            new CatalogImportPlanRow<ParsedOptionRow>(row, CatalogImportRowStatus.New, null, [])).ToList();

        var supplierMappingRows = package.SupplierMappings.Select(row =>
            new CatalogImportPlanRow<ParsedSupplierMappingRow>(row, CatalogImportRowStatus.New, null, [])).ToList();

        return new CatalogImportPlan(
            categoryRows, productRows, variantRows, codeRows, optionGroupRows, optionRows, supplierMappingRows, warnings);
    }

    public static CatalogImportValidationReport ToReport(
        Guid uploadId, Domain.Enums.CatalogPackageFormat format, CatalogImportEntityType entityType, CatalogImportPlan plan)
    {
        var rows = new List<CatalogImportRowResult>();
        rows.AddRange(plan.Categories.Select((r, i) => ToRowResult(i + 1, "Category", r.Row.Slug, r)));
        rows.AddRange(plan.Products.Select((r, i) => ToRowResult(i + 1, "Product", r.Row.NameEn, r)));
        rows.AddRange(plan.Variants.Select((r, i) => ToRowResult(i + 1, "Variant", r.Row.Sku, r)));
        rows.AddRange(plan.Codes.Select((r, i) => ToRowResult(i + 1, "Code", MaskCode(r.Row.Code), r)));
        rows.AddRange(plan.OptionGroups.Select((r, i) => ToRowResult(i + 1, "OptionGroup", r.Row.Key, r)));
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
        new(rowNumber, entityType, label, planRow.Status, planRow.Errors);

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
}
