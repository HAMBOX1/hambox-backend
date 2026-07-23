using ClosedXML.Excel;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>
/// Generates the four downloadable xlsx templates (Products/Categories/Inventory/Codes): a header
/// row matching <see cref="CatalogImportParser"/>'s column names, a couple of sample rows, and a
/// second "Instructions" sheet documenting every column. Mirrors
/// <c>ReportDocumentGenerator</c>'s ClosedXML style (bold header, <c>AdjustToContents</c>).
/// </summary>
internal sealed class CatalogImportTemplateGenerator : IImportTemplateGenerator
{
    public byte[] Generate(CatalogImportEntityType entityType)
    {
        using var workbook = new XLWorkbook();

        var (sheetName, columns, sampleRows) = entityType switch
        {
            CatalogImportEntityType.Categories => ("Categories", CategoryColumns, CategorySamples),
            CatalogImportEntityType.Products => ("Products", ProductColumns, ProductSamples),
            CatalogImportEntityType.Inventory => ("Inventory", InventoryColumns, InventorySamples),
            CatalogImportEntityType.Codes => ("Codes", CodeColumns, CodeSamples),
            _ => throw new ArgumentOutOfRangeException(nameof(entityType)),
        };

        var sheet = workbook.Worksheets.Add(sheetName);
        for (var i = 0; i < columns.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = columns[i].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
        }

        for (var r = 0; r < sampleRows.Length; r++)
        {
            for (var c = 0; c < sampleRows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = sampleRows[r][c];
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell(1, 1).Value = "Column";
        instructions.Cell(1, 2).Value = "Required";
        instructions.Cell(1, 3).Value = "Description";
        instructions.Range(1, 1, 1, 3).Style.Font.Bold = true;

        for (var i = 0; i < columns.Length; i++)
        {
            instructions.Cell(i + 2, 1).Value = columns[i].Name;
            instructions.Cell(i + 2, 2).Value = columns[i].Required ? "Yes" : "No";
            instructions.Cell(i + 2, 3).Value = columns[i].Description;
        }

        instructions.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record ColumnSpec(string Name, bool Required, string Description);

    private static readonly ColumnSpec[] CategoryColumns =
    [
        new("Slug", true, "Unique URL-friendly identifier, e.g. 'gift-cards'. Used to detect duplicates on re-import."),
        new("NameEn", true, "Category name in English."),
        new("NameAr", false, "Category name in Arabic. Falls back to NameEn if left blank."),
        new("ParentSlug", false, "Slug of the parent category, if any. Must exist elsewhere in this file or already in the catalog."),
        new("IsActive", false, "TRUE or FALSE. Defaults to TRUE."),
        new("SortOrder", false, "Integer display order among sibling categories. Defaults to 0."),
    ];

    private static readonly string[][] CategorySamples =
    [
        ["gift-cards", "Gift Cards", "بطاقات الهدايا", "", "TRUE", "0"],
        ["steam-gift-cards", "Steam Gift Cards", "بطاقات ستيم", "gift-cards", "TRUE", "1"],
    ];

    private static readonly ColumnSpec[] ProductColumns =
    [
        new("ImportKey", false, "Any unique value used only to link this product to Inventory/Codes rows in a separate file. Defaults to NameEn."),
        new("NameEn", true, "Product name in English."),
        new("NameAr", false, "Product name in Arabic. Falls back to NameEn."),
        new("DescriptionEn", false, "Product description in English."),
        new("DescriptionAr", false, "Product description in Arabic. Falls back to DescriptionEn."),
        new("Price", true, "Base price in USD (the sole stored currency). Must not be negative."),
        new("CategorySlug", true, "Slug of an existing (or in-file) category."),
        new("Status", false, "Draft, Active, Inactive, or Archived. Defaults to Draft."),
        new("StockQuantity", false, "Initial stock quantity. Defaults to 100."),
        new("AdditionalCategorySlugs", false, "Comma-separated slugs for cross-listing, e.g. 'sale,featured'."),
    ];

    private static readonly string[][] ProductSamples =
    [
        ["steam-50", "Steam Wallet Code $50", "", "50 USD Steam Wallet top-up", "", "50", "steam-gift-cards", "Active", "500", ""],
    ];

    private static readonly ColumnSpec[] InventoryColumns =
    [
        new("Sku", true, "Unique variant SKU. Used to detect duplicates on re-import."),
        new("ProductImportKey", true, "The owning product's ImportKey or NameEn."),
        new("PriceOverride", false, "Overrides the product's base price for this variant, if set."),
        new("ComparePrice", false, "Optional 'was' price shown struck-through."),
        new("Status", false, "Draft, Active, Inactive, or Archived. Defaults to Draft."),
        new("LowStockThreshold", false, "Codes-remaining threshold for a low-stock warning. Defaults to 5."),
        new("SelectedOptionValues", false, "Comma-separated option values this variant represents, if the product has option groups."),
    ];

    private static readonly string[][] InventorySamples =
    [
        ["STEAM-50-USD", "steam-50", "", "", "Active", "10", ""],
    ];

    private static readonly ColumnSpec[] CodeColumns =
    [
        new("VariantSku", true, "SKU of an existing (or in-file) variant."),
        new("Code", true, "The digital code/key value. Stored encrypted at rest either way."),
        new("SerialNumber", false, "Optional serial number."),
        new("Pin", false, "Optional PIN."),
        new("PurchaseCost", false, "What this code cost to acquire, for margin reporting."),
        new("Currency", false, "3-letter currency code for PurchaseCost. Defaults to USD."),
        new("ExpirationDate", false, "ISO 8601 date, if the code expires."),
        new("BatchName", false, "Groups codes into a named purchase batch. Defaults to an auto-generated name."),
    ];

    private static readonly string[][] CodeSamples =
    [
        ["STEAM-50-USD", "XXXXX-XXXXX-XXXXX", "", "", "45.00", "USD", "", "Import Batch"],
    ];
}
