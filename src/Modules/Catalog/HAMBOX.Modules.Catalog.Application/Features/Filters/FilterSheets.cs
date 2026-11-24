using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Filters;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Filters;

/// <summary>One thing that stops a sheet from being applied, or that the admin should know about.</summary>
public sealed record FilterImportIssue(int Row, string Message);

/// <summary>One product's filter values before and after the import.</summary>
public sealed record FilterImportChange(Guid ProductId, string ProductName, string FilterKey, string OldValues, string NewValues);

public sealed record FilterImportPreviewDto(
    int TotalRows,
    int ProductsToChange,
    int ValuesToAdd,
    int ValuesToRemove,
    IReadOnlyList<FilterImportIssue> Errors,
    IReadOnlyList<FilterImportIssue> Warnings,
    IReadOnlyList<FilterImportChange> Changes,
    IReadOnlyList<string> NewValues,
    bool Applied,
    Guid? LogId);

public sealed record FilterImportLogDto(
    Guid Id, string Kind, string Mode, string FileName, int RowCount, int ChangedCount,
    string? CreatedBy, DateTimeOffset AppliedOnUtc, DateTimeOffset? UndoneOnUtc);

internal static class FilterSheetErrors
{
    public static readonly Error LogNotFound = new("Filters.ImportNotFound", "This import was not found.");
    public static readonly Error AlreadyUndone = new("Filters.ImportAlreadyUndone", "This import was already undone.");
    public static readonly Error UndoNotSupported = new("Filters.ImportCannotUndo", "Only product tag imports can be undone.");
}

internal static class SheetText
{
    public static string Norm(string value) => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public static List<string> SplitValues(string cell) =>
        cell.Split(['|', ';', ',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool Truthy(string cell, bool fallback) =>
        string.IsNullOrWhiteSpace(cell) ? fallback : cell.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "y" or "نعم";
}

// -------------------------------------------------------------------------------------------------------------- export

/// <summary>The filter library as a sheet: one row per value (a filter with no values has one row of its own).</summary>
public sealed record ExportFilterDefinitionsQuery : IRequest<Result<FilterSheet>>;

internal sealed class ExportFilterDefinitionsQueryHandler(ICatalogDbContext db) : IRequestHandler<ExportFilterDefinitionsQuery, Result<FilterSheet>>
{
    public static readonly string[] Header =
    [
        "FilterKey", "FilterName", "FilterNameAr", "MultiSelect", "Active", "ShowWhenEmpty", "DefaultExpanded",
        "ValueKey", "ValueLabel", "ValueLabelAr", "ValueSort", "ValueActive",
    ];

    public async Task<Result<FilterSheet>> Handle(ExportFilterDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var definitions = await db.FilterDefinitions.AsNoTracking().Include(d => d.Values)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.NameEn).ToListAsync(cancellationToken);

        var rows = new List<IReadOnlyList<string>>();
        foreach (var d in definitions)
        {
            string[] head = [d.Key, d.NameEn, d.NameAr ?? "", Yes(d.IsMultiSelect), Yes(d.IsActive), Yes(d.ShowWhenEmpty), Yes(d.DefaultExpanded)];
            if (d.Values.Count == 0)
            {
                rows.Add([.. head, "", "", "", "", ""]);
            }

            foreach (var v in d.Values.OrderBy(v => v.SortOrder))
            {
                rows.Add([.. head, v.Value, v.LabelEn, v.LabelAr ?? "", v.SortOrder.ToString(), Yes(v.IsActive)]);
            }
        }

        return Result.Success(new FilterSheet(Header, rows));
    }

    private static string Yes(bool value) => value ? "yes" : "no";
}

/// <summary>
/// Products with the custom-filter values they carry (one column per filter, several values joined with "|"). With
/// <see cref="Template"/> the sheet is empty, ready to fill in.
/// </summary>
public sealed record ExportProductFiltersQuery(Guid? CategoryId, IReadOnlyList<Guid>? ProductIds, bool Template) : IRequest<Result<FilterSheet>>;

internal sealed class ExportProductFiltersQueryHandler(ICatalogDbContext db) : IRequestHandler<ExportProductFiltersQuery, Result<FilterSheet>>
{
    private const int MaxProducts = 50_000;

    public async Task<Result<FilterSheet>> Handle(ExportProductFiltersQuery request, CancellationToken cancellationToken)
    {
        var definitions = await db.FilterDefinitions.AsNoTracking().Include(d => d.Values)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.NameEn).ToListAsync(cancellationToken);
        var header = new List<string> { "ProductId", "SKU", "Name", "Category" };
        header.AddRange(definitions.Select(d => d.Key));
        if (request.Template)
        {
            return Result.Success(new FilterSheet(header, []));
        }

        var products = db.Products.AsNoTracking().Where(p => p.PendingMergeIntoProductId == null);
        if (request.ProductIds is { Count: > 0 })
        {
            var ids = request.ProductIds.Distinct().ToList();
            products = products.Where(p => ids.Contains(p.Id));
        }
        else if (request.CategoryId is { } categoryId)
        {
            var scope = await Categories.CategoryFilters.CategoryFilterResolver.DescendantScopeAsync(db, categoryId, cancellationToken);
            products = products.Where(p => scope.Contains(p.CategoryId) || p.AdditionalCategories.Any(pc => scope.Contains(pc.CategoryId)));
        }

        var list = await products.OrderBy(p => p.NameEn).Take(MaxProducts)
            .Select(p => new { p.Id, p.NameEn, p.CategoryId })
            .ToListAsync(cancellationToken);
        var productIds = list.Select(p => p.Id).ToList();

        var categoryNames = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.NameEn, cancellationToken);
        var skus = (await db.ProductVariants.AsNoTracking().Where(v => !v.IsDeleted && productIds.Contains(v.ProductId))
                .Select(v => new { v.ProductId, v.Sku }).ToListAsync(cancellationToken))
            .GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.Select(x => x.Sku).OrderBy(x => x).First());

        var tags = (await db.ProductFilterValues.AsNoTracking().Where(t => productIds.Contains(t.ProductId)).ToListAsync(cancellationToken))
            .GroupBy(t => t.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var valueLabels = definitions.SelectMany(d => d.Values).ToDictionary(v => v.Id, v => v.Value);

        var rows = new List<IReadOnlyList<string>>();
        foreach (var product in list)
        {
            var row = new List<string>
            {
                product.Id.ToString(),
                skus.GetValueOrDefault(product.Id, ""),
                product.NameEn,
                categoryNames.GetValueOrDefault(product.CategoryId, ""),
            };
            var mine = tags.GetValueOrDefault(product.Id) ?? [];
            foreach (var definition in definitions)
            {
                row.Add(string.Join("|", mine.Where(t => t.FilterId == definition.Id).Select(t => valueLabels.GetValueOrDefault(t.FilterValueId, "")).Where(v => v.Length > 0).OrderBy(v => v)));
            }

            rows.Add(row);
        }

        return Result.Success(new FilterSheet(header, rows));
    }
}

// ------------------------------------------------------------------------------------------------ import: product tags

/// <summary>
/// Checks (and with <see cref="Apply"/> applies) a sheet of product filter tags. Only the filter tags of the listed
/// products change: never prices, stock, suppliers, orders or the products themselves. Columns that are not in the sheet
/// are left alone; unknown products or values stop the import unless new values are explicitly allowed.
/// </summary>
public sealed record ImportProductFiltersCommand(
    FilterSheet Sheet,
    string FileName,
    string FileHash,
    FilterAssignMode Mode,
    bool AllowNewValues,
    bool Apply) : IRequest<Result<FilterImportPreviewDto>>;

internal sealed class ImportProductFiltersCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<ImportProductFiltersCommand, Result<FilterImportPreviewDto>>
{
    private const int MaxChangesShown = 300;
    private static readonly HashSet<string> IgnoredColumns = ["productid", "sku", "name", "productname", "category", "platform", "image"];

    private sealed record PlannedChange(Guid ProductId, string ProductName, FilterDefinition Filter, List<FilterValue> Old, List<string> NewKeys);

    public async Task<Result<FilterImportPreviewDto>> Handle(ImportProductFiltersCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<FilterImportIssue>();
        var warnings = new List<FilterImportIssue>();
        var sheet = request.Sheet;
        var headerNorm = sheet.Header.Select(SheetText.Norm).ToList();
        var idColumn = headerNorm.IndexOf("productid");
        var skuColumn = headerNorm.IndexOf("sku");
        if (idColumn < 0 && skuColumn < 0)
        {
            errors.Add(new FilterImportIssue(1, "The sheet needs a ProductId or a SKU column to find the products."));
            return Done(sheet, errors, warnings, [], [], 0, 0, 0, false, null);
        }

        var definitions = await db.FilterDefinitions.Include(d => d.Values).ToListAsync(cancellationToken);
        var filterColumns = new List<(int Index, FilterDefinition Definition)>();
        for (var c = 0; c < sheet.Header.Count; c++)
        {
            if (headerNorm[c].Length == 0 || IgnoredColumns.Contains(headerNorm[c]))
            {
                continue;
            }

            var match = definitions.FirstOrDefault(d => SheetText.Norm(d.Key) == headerNorm[c] || SheetText.Norm(d.NameEn) == headerNorm[c]);
            if (match is null)
            {
                warnings.Add(new FilterImportIssue(1, $"Column “{sheet.Header[c]}” is not one of your filters, so it was ignored."));
            }
            else if (filterColumns.Any(f => f.Definition.Id == match.Id))
            {
                errors.Add(new FilterImportIssue(1, $"Column “{sheet.Header[c]}” repeats the filter “{match.NameEn}”."));
            }
            else
            {
                filterColumns.Add((c, match));
            }
        }

        if (filterColumns.Count == 0)
        {
            errors.Add(new FilterImportIssue(1, "No column of the sheet matches one of your filters."));
            return Done(sheet, errors, warnings, [], [], 0, 0, 0, false, null);
        }

        var alreadyApplied = await db.FilterImportLogs.AsNoTracking().AnyAsync(
            l => l.Kind == "Products" && l.FileHash == request.FileHash && l.Mode == request.Mode.ToString() && l.UndoneOnUtc == null, cancellationToken);
        if (alreadyApplied && request.Apply)
        {
            errors.Add(new FilterImportIssue(0, "This exact file was already applied in this mode. Undo that import first, or change the file."));
        }

        // ---- find the products
        var rowRefs = new List<(int Row, string? IdText, string? Sku)>();
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            rowRefs.Add((r + 2, idColumn >= 0 ? sheet.Rows[r][idColumn] : null, skuColumn >= 0 ? sheet.Rows[r][skuColumn] : null));
        }

        var idsWanted = rowRefs.Select(x => Guid.TryParse(x.IdText, out var g) ? g : (Guid?)null).Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();
        var skusWanted = rowRefs.Where(x => !string.IsNullOrWhiteSpace(x.Sku)).Select(x => x.Sku!.Trim().ToUpperInvariant()).Distinct().ToList();
        var productsById = new Dictionary<Guid, string>();
        foreach (var chunk in idsWanted.Chunk(1000))
        {
            var part = chunk.ToList();
            foreach (var p in await db.Products.AsNoTracking().Where(p => part.Contains(p.Id)).Select(p => new { p.Id, p.NameEn }).ToListAsync(cancellationToken))
            {
                productsById[p.Id] = p.NameEn;
            }
        }

        var productBySku = new Dictionary<string, Guid>();
        foreach (var chunk in skusWanted.Chunk(1000))
        {
            var part = chunk.ToList();
            foreach (var v in await db.ProductVariants.AsNoTracking().Where(v => !v.IsDeleted && part.Contains(v.Sku)).Select(v => new { v.Sku, v.ProductId }).ToListAsync(cancellationToken))
            {
                productBySku[v.Sku] = v.ProductId;
            }
        }

        var missingSkuProducts = productBySku.Values.Where(id => !productsById.ContainsKey(id)).Distinct().ToList();
        foreach (var chunk in missingSkuProducts.Chunk(1000))
        {
            var part = chunk.ToList();
            foreach (var p in await db.Products.AsNoTracking().Where(p => part.Contains(p.Id)).Select(p => new { p.Id, p.NameEn }).ToListAsync(cancellationToken))
            {
                productsById[p.Id] = p.NameEn;
            }
        }

        // ---- current tags of those products for the filters in the sheet
        var involvedProducts = productsById.Keys.ToList();
        var filterIds = filterColumns.Select(f => f.Definition.Id).ToList();
        var currentTags = new List<ProductFilterValue>();
        foreach (var chunk in involvedProducts.Chunk(1000))
        {
            var part = chunk.ToList();
            currentTags.AddRange(await db.ProductFilterValues.AsNoTracking().Where(t => filterIds.Contains(t.FilterId) && part.Contains(t.ProductId)).ToListAsync(cancellationToken));
        }

        var newValueLabels = new List<string>();
        var planned = new List<PlannedChange>();
        // Several rows may belong to one product (a parent and its variants, each with their own SKU): their values are
        // added together per filter. Only the same SKU / ProductId twice is a mistake.
        var seenRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wanted = new Dictionary<(Guid ProductId, Guid FilterId), List<string>>();
        var wantedFirstRow = new Dictionary<(Guid ProductId, Guid FilterId), int>();
        foreach (var (rowNumber, idText, sku) in rowRefs)
        {
            Guid? productId = null;
            if (Guid.TryParse(idText, out var parsedId) && productsById.ContainsKey(parsedId))
            {
                productId = parsedId;
            }
            else if (!string.IsNullOrWhiteSpace(sku) && productBySku.TryGetValue(sku.Trim().ToUpperInvariant(), out var bySku))
            {
                productId = bySku;
            }

            if (productId is null)
            {
                errors.Add(new FilterImportIssue(rowNumber, $"No product found for {(string.IsNullOrWhiteSpace(idText) ? "SKU " + sku : "ProductId " + idText)}."));
                continue;
            }

            if (!seenRows.Add(string.IsNullOrWhiteSpace(sku) ? idText ?? string.Empty : sku.Trim()))
            {
                errors.Add(new FilterImportIssue(rowNumber, $"“{productsById[productId.Value]}” appears more than once in the sheet."));
                continue;
            }

            foreach (var (columnIndex, definition) in filterColumns)
            {
                var cell = sheet.Rows[rowNumber - 2][columnIndex];
                var requested = SheetText.SplitValues(cell);
                if (requested.Count == 0 && request.Mode != FilterAssignMode.Replace)
                {
                    continue;
                }

                if (!definition.IsMultiSelect && requested.Count > 1)
                {
                    errors.Add(new FilterImportIssue(rowNumber, $"“{definition.NameEn}” allows one value, but the cell has {requested.Count}."));
                    continue;
                }

                var wantedKeys = new List<string>();
                foreach (var text in requested)
                {
                    var existing = definition.Values.FirstOrDefault(v =>
                        SheetText.Norm(v.Value) == SheetText.Norm(text) || SheetText.Norm(v.LabelEn) == SheetText.Norm(text) || (v.LabelAr != null && SheetText.Norm(v.LabelAr) == SheetText.Norm(text)));
                    if (existing is not null)
                    {
                        wantedKeys.Add(existing.Value);
                    }
                    else if (request.AllowNewValues)
                    {
                        var slug = FilterDefinition.NormalizeKey(text);
                        if (slug.Length == 0)
                        {
                            errors.Add(new FilterImportIssue(rowNumber, $"“{text}” is not a usable value."));
                            continue;
                        }

                        wantedKeys.Add(slug);
                        var label = $"{definition.NameEn}: {text}";
                        if (!newValueLabels.Contains(label))
                        {
                            newValueLabels.Add(label);
                        }
                    }
                    else
                    {
                        errors.Add(new FilterImportIssue(rowNumber, $"“{text}” is not a value of “{definition.NameEn}”. Add it to the filter first (or allow new values)."));
                    }
                }

                var slot = (productId.Value, definition.Id);
                if (!wanted.TryGetValue(slot, out var keys))
                {
                    wanted[slot] = keys = [];
                    wantedFirstRow[slot] = rowNumber;
                }

                keys.AddRange(wantedKeys);
            }
        }

        foreach (var ((productId, filterId), keys) in wanted)
        {
            var definition = filterColumns.First(f => f.Definition.Id == filterId).Definition;
            var wantedKeys = keys.Distinct().ToList();
            if (!definition.IsMultiSelect && wantedKeys.Count > 1)
            {
                errors.Add(new FilterImportIssue(wantedFirstRow[(productId, filterId)], $"“{definition.NameEn}” allows one value, but the rows of “{productsById[productId]}” give {wantedKeys.Count}."));
                continue;
            }

            var old = currentTags.Where(t => t.ProductId == productId && t.FilterId == definition.Id)
                .Select(t => definition.Values.FirstOrDefault(v => v.Id == t.FilterValueId)).Where(v => v is not null).Select(v => v!).ToList();
            var oldKeys = old.Select(v => v.Value).ToHashSet();
            var newKeys = request.Mode switch
            {
                FilterAssignMode.Replace => wantedKeys,
                FilterAssignMode.Remove => oldKeys.Where(k => !wantedKeys.Contains(k)).ToList(),
                _ => oldKeys.Concat(wantedKeys).Distinct().ToList(),
            };

            if (!newKeys.OrderBy(k => k).SequenceEqual(oldKeys.OrderBy(k => k)))
            {
                planned.Add(new PlannedChange(productId, productsById[productId], definition, old, newKeys));
            }
        }

        var added = 0;
        var removed = 0;
        foreach (var change in planned)
        {
            var oldKeys = change.Old.Select(v => v.Value).ToHashSet();
            added += change.NewKeys.Count(k => !oldKeys.Contains(k));
            removed += oldKeys.Count(k => !change.NewKeys.Contains(k));
        }

        FilterImportLog? log = null;
        if (request.Apply && errors.Count == 0 && planned.Count > 0)
        {
            log = await ApplyAsync(request, planned, rowRefs.Count, cancellationToken);
        }

        var changes = planned.Take(MaxChangesShown).Select(c => new FilterImportChange(
            c.ProductId, c.ProductName, c.Filter.Key,
            string.Join(" | ", c.Old.Select(v => v.LabelEn)),
            string.Join(" | ", c.NewKeys.Select(k => c.Filter.Values.FirstOrDefault(v => v.Value == k)?.LabelEn ?? k)))).ToList();

        return Done(sheet, errors, warnings, changes, newValueLabels, planned.Select(p => p.ProductId).Distinct().Count(), added, removed, log is not null, log?.Id);
    }

    private async Task<FilterImportLog> ApplyAsync(
        ImportProductFiltersCommand request, List<PlannedChange> planned, int rowCount, CancellationToken cancellationToken)
    {
        var createdValueIds = new List<Guid>();
        var undo = new List<object>();

        // New values the sheet introduced (only when explicitly allowed).
        foreach (var group in planned.GroupBy(p => p.Filter))
        {
            foreach (var key in group.SelectMany(p => p.NewKeys).Distinct().Where(k => group.Key.Values.All(v => v.Value != k)))
            {
                var value = group.Key.AddValue(key, key.Replace('-', ' '), null, group.Key.Values.Count);
                db.FilterValues.Add(value);
                createdValueIds.Add(value.Id);
            }
        }

        foreach (var change in planned)
        {
            var oldIds = change.Old.Select(v => v.Id).ToHashSet();
            var newIds = change.NewKeys.Select(k => change.Filter.Values.First(v => v.Value == k).Id).ToHashSet();
            var addIds = newIds.Where(id => !oldIds.Contains(id)).ToList();
            var removeIds = oldIds.Where(id => !newIds.Contains(id)).ToList();

            if (removeIds.Count > 0)
            {
                db.ProductFilterValues.RemoveRange(await db.ProductFilterValues
                    .Where(t => t.ProductId == change.ProductId && t.FilterId == change.Filter.Id && removeIds.Contains(t.FilterValueId))
                    .ToListAsync(cancellationToken));
            }

            foreach (var id in addIds)
            {
                db.ProductFilterValues.Add(ProductFilterValue.Create(change.ProductId, change.Filter.Id, id));
            }

            undo.Add(new { change.ProductId, FilterId = change.Filter.Id, Added = addIds, Removed = removeIds });
        }

        var log = FilterImportLog.Create(
            "Products", request.Mode.ToString(), request.FileName, request.FileHash, rowCount,
            planned.Select(p => p.ProductId).Distinct().Count(),
            JsonSerializer.Serialize(new { Changes = undo, CreatedValues = createdValueIds }), currentUser.UserId);
        db.FilterImportLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        return log;
    }

    private static Result<FilterImportPreviewDto> Done(
        FilterSheet sheet,
        List<FilterImportIssue> errors,
        List<FilterImportIssue> warnings,
        IReadOnlyList<FilterImportChange> changes,
        IReadOnlyList<string> newValues,
        int productsToChange,
        int added,
        int removed,
        bool applied,
        Guid? logId) =>
        Result.Success(new FilterImportPreviewDto(sheet.Rows.Count, productsToChange, added, removed, errors, warnings, changes, newValues, applied, logId));
}

// ---------------------------------------------------------------------------------------- import: filter definitions

/// <summary>Creates / updates filters and their values from a sheet. It never deletes a filter or a value.</summary>
public sealed record ImportFilterDefinitionsCommand(FilterSheet Sheet, string FileName, string FileHash, bool Apply)
    : IRequest<Result<FilterImportPreviewDto>>;

internal sealed class ImportFilterDefinitionsCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<ImportFilterDefinitionsCommand, Result<FilterImportPreviewDto>>
{
    public async Task<Result<FilterImportPreviewDto>> Handle(ImportFilterDefinitionsCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<FilterImportIssue>();
        var warnings = new List<FilterImportIssue>();
        var header = request.Sheet.Header.Select(SheetText.Norm).ToList();
        int Col(string name) => header.IndexOf(SheetText.Norm(name));
        var keyCol = Col("FilterKey");
        var nameCol = Col("FilterName");
        if (keyCol < 0 && nameCol < 0)
        {
            errors.Add(new FilterImportIssue(1, "The sheet needs a FilterKey (or FilterName) column."));
        }

        var groups = new Dictionary<string, List<(int Row, IReadOnlyList<string> Cells)>>();
        for (var r = 0; r < request.Sheet.Rows.Count && errors.Count == 0; r++)
        {
            var cells = request.Sheet.Rows[r];
            var raw = keyCol >= 0 && cells[keyCol].Length > 0 ? cells[keyCol] : (nameCol >= 0 ? cells[nameCol] : "");
            var key = FilterDefinition.NormalizeKey(raw);
            if (key.Length == 0)
            {
                errors.Add(new FilterImportIssue(r + 2, "The row has no filter key or name."));
                continue;
            }

            if (!groups.TryGetValue(key, out var list))
            {
                groups[key] = list = [];
            }

            list.Add((r + 2, cells));
        }

        var existing = await db.FilterDefinitions.Include(d => d.Values).ToListAsync(cancellationToken);
        var optionGroupKeys = (await db.ProductOptionGroups.AsNoTracking().Select(g => g.Key).Distinct().ToListAsync(cancellationToken)).ToHashSet();
        string Cell(IReadOnlyList<string> cells, string name) { var i = Col(name); return i >= 0 && i < cells.Count ? cells[i] : ""; }

        var created = 0;
        var updated = 0;
        var valuesAdded = 0;
        var changes = new List<FilterImportChange>();
        if (errors.Count == 0)
        {
            foreach (var (key, rows) in groups)
            {
                var first = rows[0].Cells;
                var definition = existing.FirstOrDefault(d => d.Key == key);
                var isNew = definition is null;
                if (isNew && optionGroupKeys.Contains(key))
                {
                    errors.Add(new FilterImportIssue(rows[0].Row, $"“{key}” is already the key of a product option group."));
                    continue;
                }

                var nameEn = Cell(first, "FilterName");
                if (isNew && string.IsNullOrWhiteSpace(nameEn))
                {
                    errors.Add(new FilterImportIssue(rows[0].Row, $"The new filter “{key}” needs a FilterName."));
                    continue;
                }

                if (request.Apply)
                {
                    if (isNew)
                    {
                        definition = FilterDefinition.Create(key, nameEn, Cell(first, "FilterNameAr"));
                        definition.SetSortOrder(existing.Count);
                        db.FilterDefinitions.Add(definition);
                        existing.Add(definition);
                    }

                    definition!.Update(
                        string.IsNullOrWhiteSpace(nameEn) ? definition.NameEn : nameEn,
                        string.IsNullOrWhiteSpace(Cell(first, "FilterNameAr")) ? definition.NameAr : Cell(first, "FilterNameAr"),
                        SheetText.Truthy(Cell(first, "MultiSelect"), definition.IsMultiSelect),
                        SheetText.Truthy(Cell(first, "Active"), definition.IsActive),
                        SheetText.Truthy(Cell(first, "ShowWhenEmpty"), definition.ShowWhenEmpty),
                        SheetText.Truthy(Cell(first, "DefaultExpanded"), definition.DefaultExpanded),
                        definition.GetScopeCategoryIds(),
                        currentUser.UserId);
                }

                if (isNew)
                {
                    created++;
                }
                else
                {
                    updated++;
                }

                foreach (var (row, cells) in rows)
                {
                    var label = Cell(cells, "ValueLabel");
                    var valueKeyText = Cell(cells, "ValueKey");
                    if (label.Length == 0 && valueKeyText.Length == 0)
                    {
                        continue;
                    }

                    var valueKey = FilterDefinition.NormalizeKey(valueKeyText.Length > 0 ? valueKeyText : label);
                    var value = definition?.Values.FirstOrDefault(v => v.Value == valueKey);
                    if (value is null)
                    {
                        if (label.Length == 0)
                        {
                            errors.Add(new FilterImportIssue(row, $"The new value “{valueKeyText}” needs a ValueLabel."));
                            continue;
                        }

                        valuesAdded++;
                        changes.Add(new FilterImportChange(Guid.Empty, key, key, "", label));
                        if (request.Apply)
                        {
                            var sort = int.TryParse(Cell(cells, "ValueSort"), out var s) ? s : definition!.Values.Count;
                            var added = definition!.AddValue(valueKey, label, Cell(cells, "ValueLabelAr"), sort);
                            db.FilterValues.Add(added);
                        }
                    }
                    else if (request.Apply)
                    {
                        value.Update(
                            label.Length > 0 ? label : value.LabelEn,
                            Cell(cells, "ValueLabelAr") is { Length: > 0 } ar ? ar : value.LabelAr,
                            int.TryParse(Cell(cells, "ValueSort"), out var sortOrder) ? sortOrder : value.SortOrder,
                            SheetText.Truthy(Cell(cells, "ValueActive"), value.IsActive));
                    }
                }
            }
        }

        FilterImportLog? log = null;
        if (request.Apply && errors.Count == 0)
        {
            log = FilterImportLog.Create("Definitions", "Upsert", request.FileName, request.FileHash, request.Sheet.Rows.Count, created + updated, "[]", currentUser.UserId);
            db.FilterImportLogs.Add(log);
            await db.SaveChangesAsync(cancellationToken);
        }

        warnings.Add(new FilterImportIssue(0, $"{created} filter(s) to create, {updated} to update, {valuesAdded} value(s) to add. Nothing is ever deleted by this import."));
        return Result.Success(new FilterImportPreviewDto(
            request.Sheet.Rows.Count, created + updated, valuesAdded, 0, errors, warnings, changes.Take(300).ToList(), [], log is not null, log?.Id));
    }
}

// ----------------------------------------------------------------------------------------------------- history / undo

public sealed record GetFilterImportLogsQuery : IRequest<Result<IReadOnlyList<FilterImportLogDto>>>;

internal sealed class GetFilterImportLogsQueryHandler(ICatalogDbContext db) : IRequestHandler<GetFilterImportLogsQuery, Result<IReadOnlyList<FilterImportLogDto>>>
{
    public async Task<Result<IReadOnlyList<FilterImportLogDto>>> Handle(GetFilterImportLogsQuery request, CancellationToken cancellationToken)
    {
        var logs = await db.FilterImportLogs.AsNoTracking().OrderByDescending(l => l.AppliedOnUtc).Take(50).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<FilterImportLogDto>>(logs.Select(l => new FilterImportLogDto(
            l.Id, l.Kind, l.Mode, l.FileName, l.RowCount, l.ChangedCount, l.CreatedBy, l.AppliedOnUtc, l.UndoneOnUtc)).ToList());
    }
}

/// <summary>Takes an applied product tag import back: added tags are removed, removed tags restored, values it created deleted.</summary>
public sealed record UndoFilterImportCommand(Guid LogId) : IRequest<Result>;

internal sealed class UndoFilterImportCommandHandler(ICatalogDbContext db) : IRequestHandler<UndoFilterImportCommand, Result>
{
    private sealed record UndoItem(Guid ProductId, Guid FilterId, List<Guid> Added, List<Guid> Removed);

    private sealed record UndoPayload(List<UndoItem> Changes, List<Guid> CreatedValues);

    public async Task<Result> Handle(UndoFilterImportCommand request, CancellationToken cancellationToken)
    {
        var log = await db.FilterImportLogs.FirstOrDefaultAsync(l => l.Id == request.LogId, cancellationToken);
        if (log is null)
        {
            return Result.Failure(FilterSheetErrors.LogNotFound);
        }

        if (log.UndoneOnUtc is not null)
        {
            return Result.Failure(FilterSheetErrors.AlreadyUndone);
        }

        if (log.Kind != "Products")
        {
            return Result.Failure(FilterSheetErrors.UndoNotSupported);
        }

        var payload = JsonSerializer.Deserialize<UndoPayload>(log.UndoJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new UndoPayload([], []);
        foreach (var item in payload.Changes)
        {
            if (item.Added.Count > 0)
            {
                db.ProductFilterValues.RemoveRange(await db.ProductFilterValues
                    .Where(t => t.ProductId == item.ProductId && t.FilterId == item.FilterId && item.Added.Contains(t.FilterValueId)).ToListAsync(cancellationToken));
            }

            foreach (var valueId in item.Removed)
            {
                if (!await db.ProductFilterValues.AnyAsync(t => t.ProductId == item.ProductId && t.FilterValueId == valueId, cancellationToken))
                {
                    db.ProductFilterValues.Add(ProductFilterValue.Create(item.ProductId, item.FilterId, valueId));
                }
            }
        }

        if (payload.CreatedValues.Count > 0)
        {
            db.ProductFilterValues.RemoveRange(await db.ProductFilterValues.Where(t => payload.CreatedValues.Contains(t.FilterValueId)).ToListAsync(cancellationToken));
            db.FilterValues.RemoveRange(await db.FilterValues.Where(v => payload.CreatedValues.Contains(v.Id)).ToListAsync(cancellationToken));
        }

        log.MarkUndone();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
