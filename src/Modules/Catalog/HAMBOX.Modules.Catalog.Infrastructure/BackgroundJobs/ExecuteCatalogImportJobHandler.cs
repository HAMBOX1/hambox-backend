using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Collections;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Packaging;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Infrastructure.BackgroundJobs;

/// <summary>
/// Applies a validated <see cref="ParsedCatalogPackage"/> to the database inside one transaction —
/// Catalog-only writes use a plain <see cref="CatalogDbContext"/> transaction (one schema, already
/// atomic); a package that also carries supplier mappings additionally enlists Suppliers'
/// <c>SuppliersDbContext</c> via <see cref="ICatalogSuppliersTransactionService"/>. Any unhandled
/// exception rolls back everything — "never leave partial imports" is enforced here, not by best effort.
/// </summary>
internal sealed class ExecuteCatalogImportJobHandler(
    IBackgroundJobSerializer serializer,
    CatalogDbContext catalogDb,
    ISuppliersDbContext suppliersDb,
    ICatalogSuppliersTransactionService suppliersTransaction,
    IFileStorage fileStorage,
    ICatalogImportParser parser)
    : BackgroundJobHandlerBase<ExecuteCatalogImportJobPayload>(serializer)
{
    public override string JobType => CatalogJobTypes.ImportPackage;

    public override async Task HandleAsync(
        ExecuteCatalogImportJobPayload payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var job = await catalogDb.CatalogPackageJobs.FirstOrDefaultAsync(j => j.Id == payload.PackageJobId, cancellationToken)
            ?? throw new InvalidOperationException($"CatalogPackageJob '{payload.PackageJobId}' was not found.");

        var summary = new ImportRunSummary();

        try
        {
            ParsedCatalogPackage package;
            await using (var stream = await fileStorage.OpenReadAsync(job.StorageKey, cancellationToken))
            {
                package = await parser.ParseAsync(stream, job.Format, job.EntityType, payload.PackagePassword, cancellationToken);
            }

            job.ReportProgress(20);
            await context.ReportProgressAsync(20, cancellationToken);

            var plan = await CatalogImportMatcher.BuildPlanAsync(package, catalogDb, cancellationToken);

            var hasSupplierWrites = payload.Options.IncludeSuppliers && plan.SupplierMappings.Count > 0;

            if (hasSupplierWrites)
            {
                await suppliersTransaction.ExecuteAsync(
                    ct => ApplyPlanAsync(job, plan, payload.Strategy, payload.Options, summary, context, ct), cancellationToken);
            }
            else
            {
                await using var transaction = await catalogDb.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await ApplyPlanAsync(job, plan, payload.Strategy, payload.Options, summary, context, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            var resultSummary = new CatalogPackageSummary(summary.Created, summary.Updated, summary.Skipped, summary.Failed, summary.Errors);
            job.MarkCompleted(null, null, JsonSerializer.Serialize(resultSummary));
            await catalogDb.SaveChangesAsync(cancellationToken);
            await context.ReportProgressAsync(100, cancellationToken);
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message);
            await catalogDb.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task ApplyPlanAsync(
        CatalogPackageJob job,
        CatalogImportPlan plan,
        CatalogDuplicateStrategy strategy,
        CatalogPackageOptions options,
        ImportRunSummary summary,
        IBackgroundJobContext context,
        CancellationToken cancellationToken)
    {
        var slugToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var productKeyToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var skuToVariantId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var takenSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var takenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ---------- Categories (parents before children — iterative pass) ----------
        if (options.IncludeCategories)
        {
            (await catalogDb.Categories.AsNoTracking().Select(c => new { c.Id, c.Slug }).ToListAsync(cancellationToken))
                .ForEach(c => { slugToId[c.Slug] = c.Id; takenSlugs.Add(c.Slug); });

            var remaining = new List<CatalogImportPlanRow<ParsedCategoryRow>>(plan.Categories.Where(r => r.Status != CatalogImportRowStatus.Invalid));
            var progressed = true;
            while (remaining.Count > 0 && progressed)
            {
                progressed = false;
                foreach (var planRow in remaining.ToList())
                {
                    var row = planRow.Row;
                    if (!string.IsNullOrEmpty(row.ParentSlug) && !slugToId.ContainsKey(row.ParentSlug))
                    {
                        continue; // parent not resolved yet — retry next pass
                    }

                    Guid? parentId = string.IsNullOrEmpty(row.ParentSlug) ? null : slugToId[row.ParentSlug];

                    if (planRow.ExistingId is { } existingId && strategy != CatalogDuplicateStrategy.Rename)
                    {
                        if (strategy == CatalogDuplicateStrategy.Skip)
                        {
                            summary.Skipped++;
                        }
                        else
                        {
                            var existing = await catalogDb.Categories.FirstAsync(c => c.Id == existingId, cancellationToken);
                            existing.Update(row.NameAr ?? row.NameEn, row.NameEn, row.Slug, parentId);
                            summary.Updated++;
                        }

                        slugToId[row.Slug] = existingId;
                    }
                    else
                    {
                        var slug = planRow.ExistingId is not null
                            ? MakeUnique(row.Slug, takenSlugs)
                            : row.Slug;

                        var category = Category.Create(row.NameAr ?? row.NameEn, row.NameEn, slug, parentId);
                        catalogDb.Categories.Add(category);
                        slugToId[row.Slug] = category.Id;
                        slugToId[slug] = category.Id;
                        takenSlugs.Add(slug);
                        summary.Created++;
                    }

                    remaining.Remove(planRow);
                    progressed = true;
                }
            }

            foreach (var stuck in remaining)
            {
                summary.Failed++;
                summary.Errors.Add($"Category '{stuck.Row.Slug}': parent category could not be resolved.");
            }

            await catalogDb.SaveChangesAsync(cancellationToken);
        }

        // ---------- Collections (parents before children — iterative pass, mirrors Categories).
        // ponytail: parent lookup keyed by flat Name (last-write-wins on duplicate names across
        // different parents) rather than a fully-qualified path — collections are informal tags,
        // not a strict namespace like Category.Slug; revisit if same-name collisions cause confusion. ----------
        if (options.IncludeCollections)
        {
            var nameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            (await catalogDb.ProductCollections.AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken))
                .ForEach(c => nameToId[c.Name] = c.Id);

            var remainingCollections = new List<CatalogImportPlanRow<ParsedCollectionRow>>(
                plan.Collections.Where(r => r.Status != CatalogImportRowStatus.Invalid));
            var progressedCollections = true;
            while (remainingCollections.Count > 0 && progressedCollections)
            {
                progressedCollections = false;
                foreach (var planRow in remainingCollections.ToList())
                {
                    var row = planRow.Row;
                    if (!string.IsNullOrEmpty(row.ParentName) && !nameToId.ContainsKey(row.ParentName))
                    {
                        continue; // parent not resolved yet — retry next pass
                    }

                    Guid? parentId = string.IsNullOrEmpty(row.ParentName) ? null : nameToId[row.ParentName];

                    if (planRow.ExistingId is { } existingId && strategy != CatalogDuplicateStrategy.Rename)
                    {
                        if (strategy == CatalogDuplicateStrategy.Skip)
                        {
                            summary.Skipped++;
                        }
                        else
                        {
                            var existing = await catalogDb.ProductCollections.FirstAsync(c => c.Id == existingId, cancellationToken);
                            existing.Update(row.Name, row.Description, row.Color, row.Icon, parentId, row.SortOrder);
                            summary.Updated++;
                        }

                        nameToId[row.Name] = existingId;
                    }
                    else
                    {
                        var collection = ProductCollection.Create(row.Name, row.Description, row.Color, row.Icon, parentId, row.SortOrder);
                        catalogDb.ProductCollections.Add(collection);
                        nameToId[row.Name] = collection.Id;
                        summary.Created++;
                    }

                    remainingCollections.Remove(planRow);
                    progressedCollections = true;
                }
            }

            foreach (var stuck in remainingCollections)
            {
                summary.Failed++;
                summary.Errors.Add($"Collection '{stuck.Row.Name}': parent collection could not be resolved.");
            }

            await catalogDb.SaveChangesAsync(cancellationToken);
        }

        job.ReportProgress(40);
        await context.ReportProgressAsync(40, cancellationToken);

        // ---------- Products (always processed — the base entity for both formats) ----------
        {
            foreach (var planRow in plan.Products)
            {
                var row = planRow.Row;
                if (planRow.Status == CatalogImportRowStatus.Invalid)
                {
                    summary.Failed++;
                    summary.Errors.Add($"Product '{row.NameEn}': {string.Join(' ', planRow.Errors)}");
                    continue;
                }

                if (!slugToId.TryGetValue(row.CategorySlug, out var categoryId))
                {
                    summary.Failed++;
                    summary.Errors.Add($"Product '{row.NameEn}': category '{row.CategorySlug}' could not be resolved.");
                    continue;
                }

                if (planRow.ExistingId is { } existingId && strategy != CatalogDuplicateStrategy.Rename)
                {
                    productKeyToId[row.ImportKey] = existingId;
                    if (strategy == CatalogDuplicateStrategy.Skip)
                    {
                        summary.Skipped++;
                    }
                    else
                    {
                        var existing = await catalogDb.Products.FirstAsync(p => p.Id == existingId, cancellationToken);
                        var descriptionEn = strategy == CatalogDuplicateStrategy.Merge
                            ? (string.IsNullOrWhiteSpace(row.DescriptionEn) ? existing.DescriptionEn : row.DescriptionEn)
                            : row.DescriptionEn ?? existing.DescriptionEn;
                        existing.Update(row.NameAr ?? existing.NameAr, row.NameEn, row.DescriptionAr ?? existing.DescriptionAr, descriptionEn);
                        existing.ChangePrice(row.Price);
                        summary.Updated++;
                    }
                }
                else
                {
                    var product = Product.Create(
                        row.NameAr ?? row.NameEn, row.NameEn, row.DescriptionAr ?? string.Empty, row.DescriptionEn ?? string.Empty,
                        row.Price, categoryId);
                    product.SetInitialStock(row.StockQuantity);
                    catalogDb.Products.Add(product);
                    productKeyToId[row.ImportKey] = product.Id;
                    summary.Created++;
                }
            }

            await catalogDb.SaveChangesAsync(cancellationToken);
        }

        job.ReportProgress(60);
        await context.ReportProgressAsync(60, cancellationToken);

        // ---------- Variants ----------
        if (options.IncludeVariants)
        {
            (await catalogDb.ProductVariants.AsNoTracking().Select(v => new { v.Id, v.Sku }).ToListAsync(cancellationToken))
                .ForEach(v => { skuToVariantId[v.Sku] = v.Id; takenSkus.Add(v.Sku); });

            foreach (var planRow in plan.Variants)
            {
                var row = planRow.Row;
                if (planRow.Status == CatalogImportRowStatus.Invalid)
                {
                    summary.Failed++;
                    summary.Errors.Add($"Variant '{row.Sku}': {string.Join(' ', planRow.Errors)}");
                    continue;
                }

                if (!productKeyToId.TryGetValue(row.ProductImportKey, out var productId))
                {
                    summary.Failed++;
                    summary.Errors.Add($"Variant '{row.Sku}': product '{row.ProductImportKey}' could not be resolved.");
                    continue;
                }

                if (planRow.ExistingId is { } existingId && strategy != CatalogDuplicateStrategy.Rename)
                {
                    skuToVariantId[row.Sku] = existingId;
                    if (strategy == CatalogDuplicateStrategy.Skip)
                    {
                        summary.Skipped++;
                    }
                    else
                    {
                        var existing = await catalogDb.ProductVariants.FirstAsync(v => v.Id == existingId, cancellationToken);
                        var status = Enum.TryParse<ProductVariantStatus>(row.Status, out var parsedStatus) ? parsedStatus : existing.Status;
                        existing.Update(
                            row.Sku, existing.PlanId, row.PriceOverride ?? existing.PriceOverride, row.ComparePrice ?? existing.ComparePrice,
                            existing.SortOrder, status, existing.IsVisible, existing.MembershipPlanId, row.LowStockThreshold);
                        summary.Updated++;
                    }
                }
                else
                {
                    var sku = planRow.ExistingId is not null ? MakeUnique(row.Sku, takenSkus) : row.Sku;
                    var variant = ProductVariant.Create(productId, sku, null, row.PriceOverride, row.ComparePrice, 0, row.LowStockThreshold);
                    catalogDb.ProductVariants.Add(variant);
                    skuToVariantId[row.Sku] = variant.Id;
                    takenSkus.Add(sku);
                    summary.Created++;
                }
            }

            await catalogDb.SaveChangesAsync(cancellationToken);
        }

        job.ReportProgress(75);
        await context.ReportProgressAsync(75, cancellationToken);

        // ---------- Digital codes ----------
        if (options.IncludeDigitalCodes)
        {
            var batchByVariantAndName = new Dictionary<(Guid VariantId, string Name), InventoryBatch>();

            foreach (var planRow in plan.Codes)
            {
                var row = planRow.Row;
                if (planRow.Status == CatalogImportRowStatus.Invalid)
                {
                    summary.Failed++;
                    summary.Errors.Add($"Code for '{row.VariantSku}': {string.Join(' ', planRow.Errors)}");
                    continue;
                }

                if (planRow.Status == CatalogImportRowStatus.Duplicate)
                {
                    // A digital code's value is its identity — Skip/Update/Merge all mean "leave it alone";
                    // only Rename (treated as "import as a new code anyway") re-adds it.
                    if (strategy != CatalogDuplicateStrategy.Rename)
                    {
                        summary.Skipped++;
                        continue;
                    }
                }

                if (!skuToVariantId.TryGetValue(row.VariantSku, out var variantId))
                {
                    summary.Failed++;
                    summary.Errors.Add($"Code for '{row.VariantSku}': variant could not be resolved.");
                    continue;
                }

                var batchName = row.BatchName ?? "Imported";
                if (!batchByVariantAndName.TryGetValue((variantId, batchName), out var batch))
                {
                    batch = InventoryBatch.Create(variantId, batchName, null, null, row.Currency, row.PurchaseCost ?? 0);
                    catalogDb.InventoryBatches.Add(batch);
                    batchByVariantAndName[(variantId, batchName)] = batch;
                }

                var code = DigitalInventoryCode.Create(
                    variantId, batch.Id, row.Code, null, row.SerialNumber, row.Pin, row.PurchaseCost, null, row.Currency,
                    null, row.ExpirationDate);
                catalogDb.DigitalInventoryCodes.Add(code);
                batch.RecordImport(1);
                summary.Created++;
            }

            await catalogDb.SaveChangesAsync(cancellationToken);
        }

        job.ReportProgress(90);
        await context.ReportProgressAsync(90, cancellationToken);

        // ---------- Supplier mappings (best-effort: missing supplier = non-fatal warning) ----------
        if (options.IncludeSuppliers)
        {
            var supplierCodes = plan.SupplierMappings.Select(r => r.Row.SupplierCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var supplierIdByCode = await suppliersDb.Suppliers.AsNoTracking()
                .Where(s => supplierCodes.Contains(s.Code))
                .ToDictionaryAsync(s => s.Code, s => s.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

            foreach (var planRow in plan.SupplierMappings)
            {
                var row = planRow.Row;
                if (!supplierIdByCode.TryGetValue(row.SupplierCode, out var supplierId)
                    || !productKeyToId.TryGetValue(row.ProductImportKey, out var productId))
                {
                    summary.Errors.Add($"Supplier mapping for '{row.ExternalProductId}': supplier or product not found — skipped.");
                    summary.Skipped++;
                    continue;
                }

                var mapping = SupplierProductMapping.Create(
                    supplierId, productId, row.ExternalProductId, row.ExternalSku, null, row.BuyingPrice, row.Currency, row.Priority);
                suppliersDb.SupplierProductMappings.Add(mapping);
                summary.Created++;
            }

            await suppliersDb.SaveChangesAsync(cancellationToken);
        }
    }

    private static string MakeUnique(string baseValue, HashSet<string> taken)
    {
        var candidate = $"{baseValue}-imported";
        var suffix = 2;
        while (!taken.Add(candidate))
        {
            candidate = $"{baseValue}-imported-{suffix++}";
        }

        return candidate;
    }

    private sealed class ImportRunSummary
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; } = [];
    }
}
