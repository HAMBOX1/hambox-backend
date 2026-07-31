using System.Globalization;
using ClosedXML.Excel;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>
/// Parses an uploaded file into the shared <see cref="ParsedCatalogPackage"/> shape.
/// <c>.hambox</c> delegates to <see cref="IHamboxPackageReader"/>. <c>.csv</c> always describes
/// exactly one <see cref="CatalogImportEntityType"/>. <c>.xlsx</c> is read by sheet name (case-
/// insensitive, matching <see cref="CatalogImportTemplateGenerator"/>'s sheet names) rather than
/// always the first sheet — this is what lets the <see cref="CatalogImportEntityType.Products"/>
/// template be one workbook with five tabs (Products/Variant Groups/Variant Options/Variants/
/// Inventory Codes) instead of five separate files, while an old single-sheet "Products" export
/// (sheet name "Products", no other tabs) still parses exactly as it did before — the extra sheets
/// are optional and default to empty lists when absent. Rows are read by column header name
/// (case-insensitive) so column order in a hand-edited template never matters.
/// </summary>
internal sealed class CatalogImportParser(IHamboxPackageReader hamboxReader) : ICatalogImportParser
{
    public async Task<ParsedCatalogPackage> ParseAsync(
        Stream fileStream,
        CatalogPackageFormat format,
        CatalogImportEntityType entityType,
        string? packagePassword,
        CancellationToken cancellationToken)
    {
        if (format == CatalogPackageFormat.Hambox)
        {
            return await hamboxReader.ReadAsync(fileStream, packagePassword, cancellationToken);
        }

        if (format == CatalogPackageFormat.Csv)
        {
            var csvRows = ReadCsvRows(fileStream);
            return ToPackage(entityType, csvRows, [], [], [], []);
        }

        using var workbook = new XLWorkbook(fileStream);

        if (entityType == CatalogImportEntityType.Products)
        {
            // Variants and Inventory Codes are deliberately not read here even if present in an
            // older-style workbook — variants are generated afterward via "Generate Variants",
            // and codes are added per-variant in the catalog admin UI, not bundled with a product import.
            var productsSheet = FindSheet(workbook, "Products") ?? workbook.Worksheets.FirstOrDefault();
            return ToPackage(
                entityType,
                ReadSheetRows(productsSheet),
                ReadSheetRows(FindSheet(workbook, "Variant Groups")),
                ReadSheetRows(FindSheet(workbook, "Variant Options")),
                [],
                []);
        }

        var sheet = FindSheet(workbook, entityType.ToString()) ?? workbook.Worksheets.FirstOrDefault();
        return ToPackage(entityType, ReadSheetRows(sheet), [], [], [], []);
    }

    private static ParsedCatalogPackage ToPackage(
        CatalogImportEntityType entityType,
        List<Dictionary<string, string>> primaryRows,
        List<Dictionary<string, string>> optionGroupRows,
        List<Dictionary<string, string>> optionRows,
        List<Dictionary<string, string>> variantRows,
        List<Dictionary<string, string>> codeRows) => entityType switch
    {
        CatalogImportEntityType.Categories => ParsedCatalogPackage.Empty with { Categories = primaryRows.Select((r, i) => ToCategoryRow(r, i)).ToList() },
        CatalogImportEntityType.Collections => ParsedCatalogPackage.Empty with { Collections = primaryRows.Select((r, i) => ToCollectionRow(r, i)).ToList() },
        CatalogImportEntityType.Products => ParsedCatalogPackage.Empty with
        {
            Products = primaryRows.Select((r, i) => ToProductRow(r, i)).ToList(),
            OptionGroups = optionGroupRows.Select((r, i) => ToOptionGroupRow(r, i)).ToList(),
            Options = optionRows.Select((r, i) => ToOptionRow(r, i)).ToList(),
            Variants = variantRows.Select((r, i) => ToVariantRow(r, i)).ToList(),
            Codes = codeRows.Select((r, i) => ToCodeRow(r, i)).ToList(),
        },
        CatalogImportEntityType.Inventory => ParsedCatalogPackage.Empty with { Variants = primaryRows.Select((r, i) => ToVariantRow(r, i)).ToList() },
        CatalogImportEntityType.Codes => ParsedCatalogPackage.Empty with { Codes = primaryRows.Select((r, i) => ToCodeRow(r, i)).ToList() },
        _ => throw new InvalidDataException($"'{entityType}' uploads must specify a single entity type."),
    };

    // ---------- tabular readers ----------

    private static IXLWorksheet? FindSheet(XLWorkbook workbook, string name) =>
        workbook.Worksheets.TryGetWorksheet(name, out var worksheet) ? worksheet : null;

    private static List<Dictionary<string, string>> ReadSheetRows(IXLWorksheet? worksheet)
    {
        if (worksheet is null)
        {
            return [];
        }

        var headerRow = worksheet.FirstRowUsed();
        if (headerRow is null)
        {
            return [];
        }

        var headers = headerRow.CellsUsed().Select(c => c.GetString().Trim()).ToList();

        var rows = new List<Dictionary<string, string>>();
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                dict[headers[i]] = row.Cell(i + 1).GetString().Trim();
            }

            if (dict.Values.Any(v => v.Length > 0))
            {
                rows.Add(dict);
            }
        }

        return rows;
    }

    private static List<Dictionary<string, string>> ReadCsvRows(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        if (lines.Count == 0)
        {
            throw new InvalidDataException("The file is empty.");
        }

        var headers = SplitCsvLine(lines[0]);
        var rows = new List<Dictionary<string, string>>();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = SplitCsvLine(line);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count && i < values.Count; i++)
            {
                dict[headers[i]] = values[i];
            }

            rows.Add(dict);
        }

        return rows;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Trim());
        return result;
    }

    // ---------- row mappers ----------

    private static string Get(Dictionary<string, string> row, string key) => row.GetValueOrDefault(key, string.Empty);

    private static ParsedCategoryRow ToCategoryRow(Dictionary<string, string> row, int index) => new(
        index + 1,
        Get(row, "Slug"),
        Get(row, "NameEn"),
        NullIfEmpty(Get(row, "NameAr")),
        NullIfEmpty(Get(row, "ParentSlug")),
        ParseBool(Get(row, "IsActive"), true),
        ParseInt(Get(row, "SortOrder"), 0));

    private static ParsedCollectionRow ToCollectionRow(Dictionary<string, string> row, int index) => new(
        index + 1,
        Get(row, "Name"),
        NullIfEmpty(Get(row, "Description")),
        NullIfEmpty(Get(row, "Color")),
        NullIfEmpty(Get(row, "Icon")),
        NullIfEmpty(Get(row, "ParentName")),
        ParseInt(Get(row, "SortOrder"), 0));

    private static ParsedProductRow ToProductRow(Dictionary<string, string> row, int index)
    {
        var nameEn = Get(row, "NameEn");
        var importKey = Get(row, "ImportKey");
        return new ParsedProductRow(
            index + 1,
            string.IsNullOrWhiteSpace(importKey) ? nameEn : importKey,
            nameEn,
            NullIfEmpty(Get(row, "NameAr")),
            NullIfEmpty(Get(row, "DescriptionEn")),
            NullIfEmpty(Get(row, "DescriptionAr")),
            ParseDecimal(Get(row, "Price")),
            Get(row, "CategorySlug"),
            NullIfEmpty(Get(row, "Status")),
            ParseInt(Get(row, "StockQuantity"), 100),
            [],
            SplitList(Get(row, "AdditionalCategorySlugs")),
            SplitList(Get(row, "CollectionNames")));
    }

    private static ParsedOptionGroupRow ToOptionGroupRow(Dictionary<string, string> row, int index) => new(
        Get(row, "ProductImportKey"),
        Get(row, "GroupKey"),
        Get(row, "DisplayName"),
        NullIfEmpty(Get(row, "ParentGroupKey")),
        ParseInt(Get(row, "SortOrder"), 0),
        ParseBool(Get(row, "IsRequired"), false),
        index + 1);

    private static ParsedOptionRow ToOptionRow(Dictionary<string, string> row, int index) => new(
        Get(row, "ProductImportKey"),
        Get(row, "GroupKey"),
        Get(row, "Value"),
        Get(row, "Label"),
        ParseInt(Get(row, "SortOrder"), 0),
        index + 1);

    private static ParsedVariantRow ToVariantRow(Dictionary<string, string> row, int index) => new(
        index + 1,
        NullIfEmpty(Get(row, "Sku")),
        Get(row, "ProductImportKey"),
        ParseNullableDecimal(Get(row, "PriceOverride")),
        ParseNullableDecimal(Get(row, "ComparePrice")),
        NullIfEmpty(Get(row, "Status")),
        ParseInt(Get(row, "LowStockThreshold"), 5),
        null,
        SplitList(Get(row, "SelectedOptionValues")));

    private static ParsedCodeRow ToCodeRow(Dictionary<string, string> row, int index) => new(
        index + 1,
        Get(row, "VariantSku"),
        Get(row, "Code"),
        NullIfEmpty(Get(row, "SerialNumber")),
        NullIfEmpty(Get(row, "Pin")),
        ParseNullableDecimal(Get(row, "PurchaseCost")),
        string.IsNullOrWhiteSpace(Get(row, "Currency")) ? "USD" : Get(row, "Currency").ToUpperInvariant(),
        ParseNullableDate(Get(row, "ExpirationDate")),
        NullIfEmpty(Get(row, "BatchName")));

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool ParseBool(string value, bool defaultValue) =>
        bool.TryParse(value, out var result) ? result : defaultValue;

    private static int ParseInt(string value, int defaultValue) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : -1m;

    private static decimal? ParseNullableDecimal(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDecimal(value);

    private static DateTimeOffset? ParseNullableDate(string value) =>
        string.IsNullOrWhiteSpace(value) ? null
            : DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result) ? result : null;

    private static List<string> SplitList(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
