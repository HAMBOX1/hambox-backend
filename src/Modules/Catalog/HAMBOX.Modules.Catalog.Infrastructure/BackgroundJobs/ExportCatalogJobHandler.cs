using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
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
using HAMBOX.Modules.Catalog.Infrastructure.Packaging;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Infrastructure.BackgroundJobs;

internal sealed class ExportCatalogJobHandler(
    IBackgroundJobSerializer serializer,
    ICatalogDbContext catalogDb,
    ISuppliersDbContext suppliersDb,
    IFileStorage fileStorage,
    IHamboxPackageWriter packageWriter,
    IPackageCryptoService crypto,
    ICatalogPackageSecretProtector secretProtector)
    : BackgroundJobHandlerBase<ExportCatalogJobPayload>(serializer)
{
    public override string JobType => CatalogJobTypes.ExportPackage;

    public override async Task HandleAsync(
        ExportCatalogJobPayload payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var job = await catalogDb.CatalogPackageJobs.FirstOrDefaultAsync(j => j.Id == payload.PackageJobId, cancellationToken)
            ?? throw new InvalidOperationException($"CatalogPackageJob '{payload.PackageJobId}' was not found.");

        try
        {
            var packagePassword = string.IsNullOrEmpty(payload.PackagePassword)
                ? payload.PackagePassword
                : secretProtector.Unprotect(payload.PackagePassword);

            var package = await BuildPackageAsync(payload, cancellationToken);
            await ReportProgress(job, context, 40, cancellationToken);

            string fileName;
            string contentType;
            byte[] fileBytes;

            switch (payload.Format)
            {
                case CatalogPackageFormat.Hambox:
                    await using (var stream = await packageWriter.WriteAsync(
                        package, payload.EncryptCodes, packagePassword, cancellationToken))
                    {
                        using var buffer = new MemoryStream();
                        await stream.CopyToAsync(buffer, cancellationToken);
                        fileBytes = buffer.ToArray();
                    }

                    if (payload.PasswordProtectPackage)
                    {
                        var encrypted = crypto.Encrypt(fileBytes, packagePassword!);
                        fileBytes = [.. PackageMagic.WholePackage, .. encrypted];
                    }

                    fileName = $"catalog-export-{DateTime.UtcNow:yyyyMMddHHmmss}.hambox";
                    contentType = "application/zip";
                    break;

                case CatalogPackageFormat.Xlsx:
                    fileBytes = BuildProductsXlsx(package.Products);
                    fileName = $"catalog-export-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    break;

                default:
                    fileBytes = BuildProductsCsv(package.Products);
                    fileName = $"catalog-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                    contentType = "text/csv";
                    break;
            }

            await ReportProgress(job, context, 80, cancellationToken);

            using var saveStream = new MemoryStream(fileBytes);
            var stored = await fileStorage.SaveAsync(saveStream, fileName, contentType, "catalog-exports", cancellationToken);

            var summary = new CatalogPackageSummary(package.Products.Count, 0, 0, 0, []);
            job.MarkCompleted(stored.StorageKey, stored.FileName, JsonSerializer.Serialize(summary));
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

    private static async Task ReportProgress(CatalogPackageJob job, IBackgroundJobContext context, int percent, CancellationToken ct)
    {
        job.ReportProgress(percent);
        await context.ReportProgressAsync(percent, ct);
    }

    private async Task<ParsedCatalogPackage> BuildPackageAsync(ExportCatalogJobPayload payload, CancellationToken ct)
    {
        var productIds = payload.ProductIds.ToHashSet();
        var options = payload.Options;

        var products = await catalogDb.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Images)
            .Include(p => p.AdditionalCategories)
            .Include(p => p.Collections)
            .ToListAsync(ct);

        var referencedCategoryIds = products.Select(p => p.CategoryId)
            .Concat(products.SelectMany(p => p.AdditionalCategories.Select(ac => ac.CategoryId)))
            .ToHashSet();

        var allCategories = options.IncludeCategories
            ? await catalogDb.Categories.AsNoTracking().ToListAsync(ct)
            : [];
        var categoriesInScope = CollectWithAncestors(allCategories, referencedCategoryIds);
        var categorySlugById = allCategories.ToDictionary(c => c.Id, c => c.Slug);

        var categoryRows = categoriesInScope.Select((c, i) => new ParsedCategoryRow(
            i + 1, c.Slug, c.NameEn, c.NameAr, c.ParentId.HasValue ? categorySlugById.GetValueOrDefault(c.ParentId.Value) : null,
            c.IsActive, c.SortOrder)).ToList();

        var referencedCollectionIds = products.SelectMany(p => p.Collections.Select(pc => pc.CollectionId)).ToHashSet();

        var allCollections = options.IncludeCollections
            ? await catalogDb.ProductCollections.AsNoTracking().ToListAsync(ct)
            : [];
        var collectionsInScope = CollectCollectionsWithAncestors(allCollections, referencedCollectionIds);
        var collectionNameById = allCollections.ToDictionary(c => c.Id, c => c.Name);

        var collectionRows = collectionsInScope.Select((c, i) => new ParsedCollectionRow(
            i + 1, c.Name, c.Description, c.Color, c.Icon,
            c.ParentId.HasValue ? collectionNameById.GetValueOrDefault(c.ParentId.Value) : null, c.SortOrder)).ToList();

        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var productRows = new List<ParsedProductRow>();
        var index = 0;
        foreach (var product in products)
        {
            index++;
            var imagePaths = new List<string>();
            if (options.IncludeImages)
            {
                foreach (var image in product.Images.OrderBy(i => i.DisplayOrder))
                {
                    var extension = Path.GetExtension(image.FileName);
                    var relativePath = $"products/{product.Id}/{image.Id}{extension}";
                    try
                    {
                        await using var imgStream = await fileStorage.OpenReadAsync(image.StorageKey, ct);
                        using var buffer = new MemoryStream();
                        await imgStream.CopyToAsync(buffer, ct);
                        images[relativePath] = buffer.ToArray();
                        imagePaths.Add(relativePath);
                    }
                    catch (FileNotFoundException)
                    {
                        // Image file missing from disk — skip it rather than fail the whole export.
                    }
                }
            }

            productRows.Add(new ParsedProductRow(
                index,
                product.Id.ToString(),
                product.NameEn,
                options.IncludeLocalization ? product.NameAr : null,
                product.DescriptionEn,
                options.IncludeLocalization ? product.DescriptionAr : null,
                product.Price,
                categorySlugById.GetValueOrDefault(product.CategoryId, string.Empty),
                product.Status.ToString(),
                options.IncludeInventory ? product.StockQuantity : 0,
                imagePaths,
                product.AdditionalCategories.Select(ac => categorySlugById.GetValueOrDefault(ac.CategoryId, string.Empty))
                    .Where(s => s.Length > 0).ToList(),
                product.Collections.Select(pc => collectionNameById.GetValueOrDefault(pc.CollectionId, string.Empty))
                    .Where(s => s.Length > 0).ToList()));
        }

        var optionGroupRows = new List<ParsedOptionGroupRow>();
        var optionRows = new List<ParsedOptionRow>();
        var variantRows = new List<ParsedVariantRow>();
        var codeRows = new List<ParsedCodeRow>();

        if (options.IncludeVariants)
        {
            var optionGroups = await catalogDb.ProductOptionGroups.AsNoTracking()
                .Where(g => productIds.Contains(g.ProductId))
                .Include(g => g.Options)
                .ToListAsync(ct);
            var optionGroupById = optionGroups.ToDictionary(g => g.Id);
            var optionById = optionGroups.SelectMany(g => g.Options).ToDictionary(o => o.Id);
            var productIdToKey = products.ToDictionary(p => p.Id, p => p.Id.ToString());

            foreach (var group in optionGroups)
            {
                var parentKey = group.ParentOptionId.HasValue && optionById.TryGetValue(group.ParentOptionId.Value, out var parentOption)
                    ? optionGroupById.GetValueOrDefault(parentOption.OptionGroupId)?.Key
                    : null;
                optionGroupRows.Add(new ParsedOptionGroupRow(
                    productIdToKey[group.ProductId], group.Key, group.DisplayName, parentKey, group.SortOrder, group.IsRequired));

                foreach (var option in group.Options)
                {
                    optionRows.Add(new ParsedOptionRow(productIdToKey[group.ProductId], group.Key, option.Value, option.Label, option.SortOrder));
                }
            }

            var variants = await catalogDb.ProductVariants.AsNoTracking()
                .Where(v => productIds.Contains(v.ProductId))
                .Include(v => v.SelectedOptions)
                .ToListAsync(ct);

            var vIndex = 0;
            foreach (var variant in variants)
            {
                vIndex++;
                var selectedValues = variant.SelectedOptions
                    .Select(so => optionById.GetValueOrDefault(so.OptionId)?.Value)
                    .Where(v => v is not null)
                    .Select(v => v!)
                    .ToList();

                variantRows.Add(new ParsedVariantRow(
                    vIndex, variant.Sku, productIdToKey.GetValueOrDefault(variant.ProductId, string.Empty),
                    variant.PriceOverride, variant.ComparePrice, variant.Status.ToString(), variant.LowStockThreshold,
                    variant.MembershipPlanId, selectedValues));
            }

            if (options.IncludeDigitalCodes)
            {
                var variantIds = variants.Select(v => v.Id).ToHashSet();
                var variantSkuById = variants.ToDictionary(v => v.Id, v => v.Sku);
                var batches = await catalogDb.InventoryBatches.AsNoTracking()
                    .Where(b => variantIds.Contains(b.VariantId)).ToDictionaryAsync(b => b.Id, ct);

                var codes = await catalogDb.DigitalInventoryCodes.AsNoTracking()
                    .Where(c => variantIds.Contains(c.VariantId))
                    .ToListAsync(ct);

                var cIndex = 0;
                foreach (var code in codes)
                {
                    cIndex++;
                    codeRows.Add(new ParsedCodeRow(
                        cIndex,
                        variantSkuById.GetValueOrDefault(code.VariantId, string.Empty),
                        code.DigitalCode,
                        code.SerialNumber,
                        code.Pin,
                        code.PurchaseCost,
                        code.Currency,
                        code.ExpirationDate,
                        batches.GetValueOrDefault(code.BatchId)?.Name));
                }
            }
        }

        var supplierMappingRows = new List<ParsedSupplierMappingRow>();
        if (options.IncludeSuppliers)
        {
            var mappings = await suppliersDb.SupplierProductMappings.AsNoTracking()
                .Where(m => productIds.Contains(m.InternalProductId))
                .ToListAsync(ct);
            var supplierIds = mappings.Select(m => m.SupplierId).ToHashSet();
            var supplierCodeById = await suppliersDb.Suppliers.AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Code, ct);
            var productKeyById = products.ToDictionary(p => p.Id, p => p.Id.ToString());

            foreach (var mapping in mappings)
            {
                if (!productKeyById.TryGetValue(mapping.InternalProductId, out var productKey)
                    || !supplierCodeById.TryGetValue(mapping.SupplierId, out var supplierCode))
                {
                    continue;
                }

                supplierMappingRows.Add(new ParsedSupplierMappingRow(
                    productKey, supplierCode, mapping.ExternalProductId, mapping.ExternalSku, mapping.BuyingPrice, mapping.Currency, mapping.Priority));
            }
        }

        return new ParsedCatalogPackage(
            categoryRows, productRows, optionGroupRows, optionRows, variantRows, codeRows, supplierMappingRows, images, payload.EncryptCodes,
            collectionRows);
    }

    private static List<Category> CollectWithAncestors(List<Category> allCategories, HashSet<Guid> seedIds)
    {
        var byId = allCategories.ToDictionary(c => c.Id);
        var result = new Dictionary<Guid, Category>();

        foreach (var id in seedIds)
        {
            var current = byId.GetValueOrDefault(id);
            while (current is not null && result.TryAdd(current.Id, current))
            {
                current = current.ParentId.HasValue ? byId.GetValueOrDefault(current.ParentId.Value) : null;
            }
        }

        return [.. result.Values];
    }

    private static List<ProductCollection> CollectCollectionsWithAncestors(List<ProductCollection> allCollections, HashSet<Guid> seedIds)
    {
        var byId = allCollections.ToDictionary(c => c.Id);
        var result = new Dictionary<Guid, ProductCollection>();

        foreach (var id in seedIds)
        {
            var current = byId.GetValueOrDefault(id);
            while (current is not null && result.TryAdd(current.Id, current))
            {
                current = current.ParentId.HasValue ? byId.GetValueOrDefault(current.ParentId.Value) : null;
            }
        }

        return [.. result.Values];
    }

    private static byte[] BuildProductsXlsx(IReadOnlyList<ParsedProductRow> products)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Products");
        string[] headers = ["ImportKey", "NameEn", "NameAr", "DescriptionEn", "DescriptionAr", "Price", "CategorySlug", "Status", "StockQuantity", "AdditionalCategorySlugs", "CollectionNames"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        for (var r = 0; r < products.Count; r++)
        {
            var p = products[r];
            sheet.Cell(r + 2, 1).Value = p.ImportKey;
            sheet.Cell(r + 2, 2).Value = p.NameEn;
            sheet.Cell(r + 2, 3).Value = p.NameAr ?? string.Empty;
            sheet.Cell(r + 2, 4).Value = p.DescriptionEn ?? string.Empty;
            sheet.Cell(r + 2, 5).Value = p.DescriptionAr ?? string.Empty;
            sheet.Cell(r + 2, 6).Value = p.Price;
            sheet.Cell(r + 2, 7).Value = p.CategorySlug;
            sheet.Cell(r + 2, 8).Value = p.Status ?? string.Empty;
            sheet.Cell(r + 2, 9).Value = p.StockQuantity;
            sheet.Cell(r + 2, 10).Value = string.Join(',', p.AdditionalCategorySlugs);
            sheet.Cell(r + 2, 11).Value = string.Join(',', p.CollectionNames ?? []);
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildProductsCsv(IReadOnlyList<ParsedProductRow> products)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ImportKey,NameEn,NameAr,DescriptionEn,DescriptionAr,Price,CategorySlug,Status,StockQuantity,AdditionalCategorySlugs,CollectionNames");
        foreach (var p in products)
        {
            builder.AppendLine(string.Join(',',
                Escape(p.ImportKey), Escape(p.NameEn), Escape(p.NameAr ?? ""), Escape(p.DescriptionEn ?? ""),
                Escape(p.DescriptionAr ?? ""), p.Price, Escape(p.CategorySlug), Escape(p.Status ?? ""),
                p.StockQuantity, Escape(string.Join(';', p.AdditionalCategorySlugs)), Escape(string.Join(';', p.CollectionNames ?? []))));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
