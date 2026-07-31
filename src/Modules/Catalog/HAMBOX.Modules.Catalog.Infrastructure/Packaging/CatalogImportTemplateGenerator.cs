using ClosedXML.Excel;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportLookups;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>
/// Generates the downloadable xlsx templates: a header row matching <see cref="CatalogImportParser"/>'s
/// column names, a couple of sample rows, dropdown-validated lookup columns sourced from a hidden
/// "Lists" sheet (Categories/Collections/Variant Groups — DB-driven, regenerated fresh on every
/// download) or a short quoted-literal list (Status/Currency/boolean columns — enum-driven, still
/// never hardcoded), and an "Instructions" sheet documenting every column. Mirrors
/// <c>ReportDocumentGenerator</c>'s ClosedXML style (bold header, <c>AdjustToContents</c>).
/// <see cref="CatalogImportEntityType.Products"/> is a five-tab workbook (Products/Variant
/// Groups/Variant Options/Variants/Inventory Codes) so a whole product graph — including variants
/// with dynamic option groups — is one upload instead of five; every other entity type stays a
/// single sheet, unchanged from before.
/// </summary>
internal sealed class CatalogImportTemplateGenerator : IImportTemplateGenerator
{
    private const int MaxDropdownRows = 1002;
    private const int CategoryListColumn = 1;
    private const int CollectionListColumn = 2;
    private const int VariantGroupListColumn = 3;
    private const int ProductStatusListColumn = 4;
    private const int VariantStatusListColumn = 5;
    private const int CurrencyListColumn = 6;

    /// <summary>
    /// Workbook-scoped defined names for the hidden Lists sheet's columns. Data validation must
    /// reference these by NAME rather than a direct cross-sheet range (e.g. <c>Lists!$A$2:$A$3</c>)
    /// — ClosedXML writes a direct cross-sheet range as an Excel-2010+ <c>x14:dataValidations</c>
    /// extension block, which real Excel frequently flags as corrupt and "repairs" by dropping the
    /// validation. A named range keeps the validation in the standard, universally-supported
    /// <c>dataValidations</c> element instead. Only Category/Collection/VariantGroup are DB-driven
    /// and need one (Status/Currency stay on the simpler quoted-literal list — see
    /// <see cref="ApplyDropdowns"/> — they're still sourced from <c>lookups</c>, never hardcoded,
    /// just enum-backed rather than table-backed, so a named range buys nothing extra for them).
    /// </summary>
    private const string CategoryListName = "CatalogImportCategoryList";
    private const string CollectionListName = "CatalogImportCollectionList";
    private const string VariantGroupListName = "CatalogImportVariantGroupList";

    public byte[] Generate(CatalogImportEntityType entityType, CatalogImportLookups lookups)
    {
        using var workbook = new XLWorkbook();
        var listsSheet = BuildListsSheet(workbook, lookups);

        (string SheetName, ColumnSpec[] Columns, string[][] Samples)[] sections = entityType switch
        {
            CatalogImportEntityType.Categories => [("Categories", CategoryColumns, CategorySamples)],
            CatalogImportEntityType.Collections => [("Collections", CollectionColumns, CollectionSamples)],
            CatalogImportEntityType.Inventory => [("Inventory", InventoryColumns, InventorySamples)],
            CatalogImportEntityType.Codes => [("Codes", CodeColumns, CodeSamples)],
            // Variants and Inventory Codes are deliberately not part of the Products bundle —
            // variants are generated afterward via the existing "Generate Variants" combinatorial
            // action (from these option groups/options), and codes are added per-variant in the
            // catalog admin UI, not bulk-imported alongside a product graph.
            CatalogImportEntityType.Products =>
            [
                ("Products", ProductColumns, ProductSamples),
                ("Variant Groups", VariantGroupColumns, VariantGroupSamples),
                ("Variant Options", VariantOptionColumns, VariantOptionSamples),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(entityType)),
        };

        foreach (var (sheetName, columns, sampleRows) in sections)
        {
            BuildSheet(workbook, sheetName, columns, sampleRows, lookups);
        }

        AddInstructionsSheet(workbook, sections);
        listsSheet.Hide();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildSheet(
        XLWorkbook workbook, string sheetName, ColumnSpec[] columns, string[][] sampleRows, CatalogImportLookups lookups)
    {
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

        ApplyDropdowns(sheet, columns, lookups);

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    /// <summary>
    /// A literal (non-reference) list source for Excel's "List" data validation must be a quoted
    /// string — <c>"A,B,C"</c>, not <c>A,B,C</c> — per the OOXML spec. ClosedXML's
    /// <c>List(string, bool)</c> does not add these quotes itself; writing the bare
    /// comma-separated value produces a <c>formula1</c> Excel's strict parser rejects as corrupt
    /// (confirmed by round-tripping through the real Excel COM API, not just ClosedXML's own
    /// lenient reader). Named-range-based dropdowns (Category/Collection) must stay unquoted —
    /// quoting a name turns it into a literal string instead of a name reference.
    /// </summary>
    private static string QuotedList(IEnumerable<string> values) => $"\"{string.Join(",", values)}\"";

    private static void ApplyDropdowns(IXLWorksheet sheet, ColumnSpec[] columns, CatalogImportLookups lookups)
    {
        for (var i = 0; i < columns.Length; i++)
        {
            var col = i + 1;
            var range = sheet.Range(2, col, MaxDropdownRows, col);
            switch (columns[i].Dropdown)
            {
                case DropdownKind.Category when lookups.Categories.Count > 0:
                    range.SetDataValidation().List(CategoryListName, true);
                    break;
                case DropdownKind.Collection when lookups.Collections.Count > 0:
                    range.SetDataValidation().List(CollectionListName, true);
                    break;
                case DropdownKind.CategoryMulti when lookups.Categories.Count > 0:
                    ApplyMultiValueList(range, CategoryListName);
                    break;
                case DropdownKind.CollectionMulti when lookups.Collections.Count > 0:
                    ApplyMultiValueList(range, CollectionListName);
                    break;
                case DropdownKind.VariantGroup when lookups.VariantGroups.Count > 0:
                    range.SetDataValidation().List(VariantGroupListName, true);
                    break;
                case DropdownKind.ProductStatus:
                    range.SetDataValidation().List(QuotedList(lookups.ProductStatuses), true);
                    break;
                case DropdownKind.VariantStatus:
                    range.SetDataValidation().List(QuotedList(lookups.VariantStatuses), true);
                    break;
                case DropdownKind.Currency:
                    range.SetDataValidation().List(QuotedList(lookups.Currencies), true);
                    break;
                case DropdownKind.Bool:
                    range.SetDataValidation().List("\"TRUE,FALSE\"", true);
                    break;
            }
        }
    }

    /// <summary>
    /// A comma-separated multi-value cell (AdditionalCategorySlugs, CollectionNames) still gets the
    /// dropdown arrow and suggestion list for picking one value quickly, but
    /// <see cref="IXLDataValidation.ShowErrorMessage"/> is turned off so typing/pasting a
    /// "A, B, C" list — which will never literally match one list entry — isn't hard-blocked by
    /// Excel's validation popup. Excel's own List validation has no native multi-select; this is
    /// the standard "advisory dropdown" workaround rather than a VBA-backed multi-select control,
    /// which the import assistant deliberately avoids (see the class doc comment).
    /// </summary>
    private static void ApplyMultiValueList(IXLRange range, string listName)
    {
        var validation = range.SetDataValidation();
        validation.List(listName, true);
        validation.ShowErrorMessage = false;
    }

    /// <summary>
    /// The one hidden sheet every dropdown (and the wizard's correction pickers) is sourced from —
    /// every column here comes straight from <see cref="CatalogImportLookups"/>, which is itself
    /// DB/enum-driven (<see cref="GetImportLookups.GetCatalogImportLookupsQueryHandler"/>), so
    /// nothing here is ever hardcoded and new categories/collections/variant groups show up the
    /// next time this template is downloaded, no code change required. Categories shows the
    /// friendly <c>NameEn</c> (<see cref="CatalogImportLookupItem.Label"/>), not the raw slug — the
    /// owner never has to know or type a slug; <see cref="CatalogImportLookupResolver"/> resolves
    /// the selected name back to its slug at validate/execute time, and a value that's already a
    /// slug (an older template) still round-trips unchanged.
    /// </summary>
    private static IXLWorksheet BuildListsSheet(XLWorkbook workbook, CatalogImportLookups lookups)
    {
        var sheet = workbook.Worksheets.Add("Lists");
        sheet.Cell(1, CategoryListColumn).Value = "Categories";
        sheet.Cell(1, CollectionListColumn).Value = "Collections";
        sheet.Cell(1, VariantGroupListColumn).Value = "Variant Groups";
        sheet.Cell(1, ProductStatusListColumn).Value = "Product Statuses";
        sheet.Cell(1, VariantStatusListColumn).Value = "Variant Statuses";
        sheet.Cell(1, CurrencyListColumn).Value = "Currencies";

        for (var i = 0; i < lookups.Categories.Count; i++)
        {
            sheet.Cell(i + 2, CategoryListColumn).Value = lookups.Categories[i].Label;
        }

        for (var i = 0; i < lookups.Collections.Count; i++)
        {
            sheet.Cell(i + 2, CollectionListColumn).Value = lookups.Collections[i].Value;
        }

        for (var i = 0; i < lookups.VariantGroups.Count; i++)
        {
            sheet.Cell(i + 2, VariantGroupListColumn).Value = lookups.VariantGroups[i].Value;
        }

        for (var i = 0; i < lookups.ProductStatuses.Count; i++)
        {
            sheet.Cell(i + 2, ProductStatusListColumn).Value = lookups.ProductStatuses[i];
        }

        for (var i = 0; i < lookups.VariantStatuses.Count; i++)
        {
            sheet.Cell(i + 2, VariantStatusListColumn).Value = lookups.VariantStatuses[i];
        }

        for (var i = 0; i < lookups.Currencies.Count; i++)
        {
            sheet.Cell(i + 2, CurrencyListColumn).Value = lookups.Currencies[i];
        }

        if (lookups.Categories.Count > 0)
        {
            workbook.DefinedNames.Add(CategoryListName, sheet.Range(2, CategoryListColumn, 1 + lookups.Categories.Count, CategoryListColumn));
        }

        if (lookups.Collections.Count > 0)
        {
            workbook.DefinedNames.Add(CollectionListName, sheet.Range(2, CollectionListColumn, 1 + lookups.Collections.Count, CollectionListColumn));
        }

        if (lookups.VariantGroups.Count > 0)
        {
            workbook.DefinedNames.Add(VariantGroupListName, sheet.Range(2, VariantGroupListColumn, 1 + lookups.VariantGroups.Count, VariantGroupListColumn));
        }

        return sheet;
    }

    private static void AddInstructionsSheet(XLWorkbook workbook, (string SheetName, ColumnSpec[] Columns, string[][] Samples)[] sections)
    {
        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell(1, 1).Value = "Sheet";
        instructions.Cell(1, 2).Value = "Column";
        instructions.Cell(1, 3).Value = "Required";
        instructions.Cell(1, 4).Value = "Description";
        instructions.Range(1, 1, 1, 4).Style.Font.Bold = true;

        var row = 2;
        foreach (var (sheetName, columns, _) in sections)
        {
            foreach (var column in columns)
            {
                instructions.Cell(row, 1).Value = sheetName;
                instructions.Cell(row, 2).Value = column.Name;
                instructions.Cell(row, 3).Value = column.Required ? "Yes" : "No";
                instructions.Cell(row, 4).Value = column.Description;
                row++;
            }
        }

        if (sections.Any(s => s.SheetName == "Variants"))
        {
            instructions.Cell(row, 1).Value = "Note";
            instructions.Cell(row, 4).Value =
                "Sku is only required if you choose the 'Use Imported SKU' strategy during import. " +
                "'Auto Generate' and 'Generate Missing Only' fill in SKUs automatically at import time.";
            row++;
            instructions.Cell(row, 1).Value = "Note";
            instructions.Cell(row, 4).Value =
                "Membership plan assignment is never migrated by import — reassign it manually afterwards.";
        }

        instructions.Columns().AdjustToContents();
    }

    private enum DropdownKind
    {
        None,
        Category,
        Collection,
        CategoryMulti,
        CollectionMulti,
        VariantGroup,
        ProductStatus,
        VariantStatus,
        Currency,
        Bool,
    }

    private sealed record ColumnSpec(string Name, bool Required, string Description, DropdownKind Dropdown = DropdownKind.None);

    private static readonly ColumnSpec[] CategoryColumns =
    [
        new("Slug", true, "Unique URL-friendly identifier, e.g. 'gift-cards'. Used to detect duplicates on re-import."),
        new("NameEn", true, "Category name in English."),
        new("NameAr", false, "Category name in Arabic. Falls back to NameEn if left blank."),
        new("ParentSlug", false, "Name of the parent category, if any — pick from the dropdown. Must exist elsewhere in this file or already in the catalog.", DropdownKind.Category),
        new("IsActive", false, "TRUE or FALSE. Defaults to TRUE.", DropdownKind.Bool),
        new("SortOrder", false, "Integer display order among sibling categories. Defaults to 0."),
    ];

    private static readonly string[][] CategorySamples =
    [
        ["gift-cards", "Gift Cards", "بطاقات الهدايا", "", "TRUE", "0"],
        ["steam-gift-cards", "Steam Gift Cards", "بطاقات ستيم", "gift-cards", "TRUE", "1"],
    ];

    private static readonly ColumnSpec[] CollectionColumns =
    [
        new("Name", true, "Collection name, e.g. 'Supplier Bamboo'. Internal-only — never shown to customers."),
        new("Description", false, "Optional internal note about what this collection is for."),
        new("Color", false, "Optional hex color for the chip, e.g. '#22C55E'."),
        new("Icon", false, "Optional PrimeIcons class name, e.g. 'pi pi-star'."),
        new("ParentName", false, "Name of the parent collection, if any. Must exist elsewhere in this file or already exist.", DropdownKind.Collection),
        new("SortOrder", false, "Integer display order among sibling collections. Defaults to 0."),
    ];

    private static readonly string[][] CollectionSamples =
    [
        ["Suppliers", "", "", "pi pi-truck", "", "0"],
        ["Supplier Bamboo", "", "#22C55E", "pi pi-truck", "Suppliers", "1"],
    ];

    private static readonly ColumnSpec[] ProductColumns =
    [
        new("ImportKey", false, "Any unique value used only to link this product to Variant Groups/Options/Variants/Inventory Codes rows in this same file. Defaults to NameEn."),
        new("NameEn", true, "Product name in English."),
        new("NameAr", false, "Product name in Arabic. Falls back to NameEn."),
        new("DescriptionEn", false, "Product description in English."),
        new("DescriptionAr", false, "Product description in Arabic. Falls back to DescriptionEn."),
        new("Price", true, "Base price in USD (the sole stored currency). Must not be negative."),
        new("CategorySlug", true, "Name of an existing (or in-file) category — pick from the dropdown. A raw slug from an older template still works too.", DropdownKind.Category),
        new("Status", false, "Draft, Active, Inactive, or Archived. Defaults to Draft.", DropdownKind.ProductStatus),
        new("StockQuantity", false, "Initial stock quantity. Defaults to 100."),
        new("AdditionalCategorySlugs", false, "Comma-separated category names for cross-listing, e.g. 'Sale, Featured'. The dropdown picks one at a time — type or paste the rest separated by commas.", DropdownKind.CategoryMulti),
        new("CollectionNames", false, "Comma-separated internal collection names, e.g. 'Featured,Supplier Bamboo'. Never customer-facing. The dropdown picks one at a time — type or paste the rest separated by commas.", DropdownKind.CollectionMulti),
    ];

    private static readonly string[][] ProductSamples =
    [
        ["steam-50", "Steam Wallet Code $50", "", "50 USD Steam Wallet top-up", "", "50", "Steam Gift Cards", "Active", "500", "", ""],
    ];

    private static readonly ColumnSpec[] VariantGroupColumns =
    [
        new("ProductImportKey", true, "The owning product's ImportKey or NameEn (see the Products sheet)."),
        new("GroupKey", true, "Unique slug for this option group within the product, e.g. 'platform'. Pick an existing group name from the dropdown to stay consistent across products, or type a new one.", DropdownKind.VariantGroup),
        new("DisplayName", true, "Customer-facing label, e.g. 'Platform'."),
        new("ParentGroupKey", false, "GroupKey of a parent group, if this group only applies once another option is chosen (e.g. 'region' under 'platform').", DropdownKind.VariantGroup),
        new("SortOrder", false, "Integer display order. Defaults to 0."),
        new("IsRequired", false, "TRUE or FALSE — must the customer choose an option from this group? Defaults to FALSE.", DropdownKind.Bool),
    ];

    private static readonly string[][] VariantGroupSamples =
    [
        ["steam-50", "platform", "Platform", "", "0", "TRUE"],
    ];

    private static readonly ColumnSpec[] VariantOptionColumns =
    [
        new("ProductImportKey", true, "The owning product's ImportKey or NameEn (see the Products sheet)."),
        new("GroupKey", true, "GroupKey this option belongs to — must match a GroupKey on the Variant Groups sheet.", DropdownKind.VariantGroup),
        new("Value", true, "Unique slug for this option within the product, e.g. 'xbox'. Referenced by Variants' SelectedOptionValues."),
        new("Label", true, "Customer-facing label, e.g. 'Xbox'."),
        new("SortOrder", false, "Integer display order. Defaults to 0."),
    ];

    private static readonly string[][] VariantOptionSamples =
    [
        ["steam-50", "platform", "global", "Global", "0"],
    ];

    private static readonly ColumnSpec[] VariantColumns =
    [
        new("Sku", false, "Unique variant SKU. Only required if the import's SKU Strategy is 'Use Imported SKU' — leave blank to auto-generate."),
        new("ProductImportKey", true, "The owning product's ImportKey or NameEn."),
        new("PriceOverride", false, "Overrides the product's base price for this variant, if set."),
        new("ComparePrice", false, "Optional 'was' price shown struck-through."),
        new("Status", false, "Draft, Active, Inactive, or Archived. Defaults to Draft.", DropdownKind.VariantStatus),
        new("LowStockThreshold", false, "Codes-remaining threshold for a low-stock warning. Defaults to 5."),
        new("SelectedOptionValues", false, "Comma-separated option values this variant represents, if the product has variant groups — see the Variant Options sheet."),
    ];

    private static readonly string[][] VariantSamples =
    [
        ["STEAM-50-USD", "steam-50", "", "", "Active", "10", "global"],
    ];

    private static readonly ColumnSpec[] InventoryColumns = VariantColumns;

    private static readonly string[][] InventorySamples = VariantSamples;

    private static readonly ColumnSpec[] CodeColumns =
    [
        new("VariantSku", true, "SKU of an existing (or in-file) variant."),
        new("Code", true, "The digital code/key value. Stored encrypted at rest either way."),
        new("SerialNumber", false, "Optional serial number."),
        new("Pin", false, "Optional PIN."),
        new("PurchaseCost", false, "What this code cost to acquire, for margin reporting."),
        new("Currency", false, "3-letter currency code for PurchaseCost. Defaults to USD.", DropdownKind.Currency),
        new("ExpirationDate", false, "ISO 8601 date, if the code expires."),
        new("BatchName", false, "Groups codes into a named purchase batch. Defaults to an auto-generated name."),
    ];

    private static readonly string[][] CodeSamples =
    [
        ["STEAM-50-USD", "XXXXX-XXXXX-XXXXX", "", "", "45.00", "USD", "", "Import Batch"],
    ];
}
