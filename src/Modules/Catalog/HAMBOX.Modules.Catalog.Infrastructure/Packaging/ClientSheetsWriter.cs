using ClosedXML.Excel;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportLookups;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>
/// Writes the client's sheet layouts (Categories, Products, Pricing, Stock): as empty templates to fill in, and as the
/// export of the catalog. <see cref="ClientSheets"/> reads the same layouts back.
/// </summary>
internal static class ClientSheetsWriter
{
    public static readonly string[] CategoryHeaders =
        ["NameEN", "DescriptionEN", "Status", "CategoryType", "ParentCategoryKey", "FilterGroups", "ExcludedFilterGroups", "Slug", "NameAR", "DescriptionAR", "CategoryKey", "SortOrder"];

    // "VariantOptions" is ours: it names each variant's option per group, so a re-import never has to guess it from the name.
    public static readonly string[] ProductHeaders =
        ["SKU", "NameEN", "DescriptionEN", "Status", "RecordType", "ParentSKU", "VariantGroups", "AdditionalDescriptionEN", "PrimaryCategory", "AdditionalCategories", "InternalCategories", "MainImageRef", "NameAR", "DescriptionAR", "AdditionalDescriptionAR", "VariantOptions"];

    public static readonly string[] PricingHeaders =
        ["Product Name", "Currency", "Cost Price", "Normal Price", "Sale Price", "Elite Price", "Normal Margin %", "Sale Margin %", "Elite Margin %", "Sale Start", "Sale End", "SKU"];

    public static readonly string[] StockHeaders = ["SKU", "ProductName", "EntryMode", "StockData", "InternalNote"];

    private const int PricingFirstRow = 3;
    private const int PricingTemplateRows = 500;
    private const int DropdownLastRow = 2000;
    private const int MaxCodesPerCell = 500;

    private static readonly string[] CategoryStatuses = ["Draft", "Active", "Inactive"];
    private static readonly string[] CategoryTypes = ["Public", "Internal"];
    private static readonly string[] RecordTypes = ["Parent", "Variant"];
    private static readonly string[] EntryModes = ["PerLine", "WholeEntry"];

    // ---------- templates ----------

    public static bool Handles(CatalogImportEntityType entityType) =>
        entityType is CatalogImportEntityType.Categories or CatalogImportEntityType.Products
            or CatalogImportEntityType.Pricing or CatalogImportEntityType.Codes;

    public static byte[] Template(CatalogImportEntityType entityType, CatalogImportLookups lookups)
    {
        using var workbook = new XLWorkbook();
        switch (entityType)
        {
            case CatalogImportEntityType.Categories:
                var categories = Header(workbook.Worksheets.Add("Categories"), CategoryHeaders);
                Dropdown(categories, 3, CategoryStatuses);
                Dropdown(categories, 4, CategoryTypes);
                Instructions(workbook, CategoryNotes);
                break;
            case CatalogImportEntityType.Products:
                var products = Header(workbook.Worksheets.Add("Products"), ProductHeaders);
                Dropdown(products, 4, lookups.ProductStatuses);
                Dropdown(products, 5, RecordTypes);
                Instructions(workbook, ProductNotes);
                break;
            case CatalogImportEntityType.Pricing:
                var pricing = workbook.Worksheets.Add("Pricing");
                PricingHeader(pricing);
                for (var row = PricingFirstRow; row < PricingFirstRow + PricingTemplateRows; row++)
                {
                    pricing.Cell(row, 2).Value = "USD";
                    PricingRowFormat(pricing, row);
                }

                pricing.Range(PricingFirstRow, 2, DropdownLastRow, 2).CreateDataValidation().List(Quoted(lookups.Currencies), true);
                Instructions(workbook, PricingNotes);
                break;
            default:
                var stock = Header(workbook.Worksheets.Add("Stock"), StockHeaders);
                Dropdown(stock, 3, EntryModes);
                Instructions(workbook, StockNotes);
                break;
        }

        return Save(workbook);
    }

    // ---------- export ----------

    public static byte[] Workbook(ParsedCatalogPackage package)
    {
        using var workbook = new XLWorkbook();
        WriteCategories(Header(workbook.Worksheets.Add("Categories"), CategoryHeaders), package);
        var variantNames = WriteProducts(Header(workbook.Worksheets.Add("Products"), ProductHeaders), package);

        var pricing = workbook.Worksheets.Add("Pricing");
        PricingHeader(pricing);
        var prices = package.Prices ?? [];
        for (var i = 0; i < prices.Count; i++)
        {
            var p = prices[i];
            var row = PricingFirstRow + i;
            pricing.Cell(row, 1).Value = p.ProductName ?? variantNames.GetValueOrDefault(p.Sku, string.Empty);
            pricing.Cell(row, 2).Value = p.Currency;
            SetAmount(pricing.Cell(row, 3), p.CostPrice);
            SetAmount(pricing.Cell(row, 4), p.NormalPrice);
            SetAmount(pricing.Cell(row, 5), p.SalePrice);
            SetAmount(pricing.Cell(row, 6), p.ElitePrice);
            if (p.SaleStart is { } start)
            {
                pricing.Cell(row, 10).Value = start.UtcDateTime.Date;
            }

            if (p.SaleEnd is { } end)
            {
                pricing.Cell(row, 11).Value = end.UtcDateTime.Date;
            }

            pricing.Cell(row, 12).Value = p.Sku;
            PricingRowFormat(pricing, row);
        }

        if (package.Codes.Count > 0)
        {
            WriteStock(Header(workbook.Worksheets.Add("Stock"), StockHeaders), package, variantNames);
        }

        return Save(workbook);
    }

    private static void WriteCategories(IXLWorksheet sheet, ParsedCatalogPackage package)
    {
        for (var i = 0; i < package.Categories.Count; i++)
        {
            var c = package.Categories[i];
            var row = i + 2;
            sheet.Cell(row, 1).Value = c.NameEn;
            sheet.Cell(row, 2).Value = c.DescriptionEn ?? string.Empty;
            sheet.Cell(row, 3).Value = c.Status ?? (c.IsActive ? "Active" : "Inactive");
            sheet.Cell(row, 4).Value = "Public";
            sheet.Cell(row, 5).Value = c.ParentSlug ?? string.Empty;
            sheet.Cell(row, 6).Value = string.Join(";", c.FilterGroups ?? []);
            sheet.Cell(row, 7).Value = string.Join(";", c.ExcludedFilterGroups ?? []);
            sheet.Cell(row, 8).Value = c.Slug;
            sheet.Cell(row, 9).Value = c.NameAr ?? string.Empty;
            sheet.Cell(row, 10).Value = c.DescriptionAr ?? string.Empty;
            sheet.Cell(row, 11).Value = c.Slug;
            sheet.Cell(row, 12).Value = c.SortOrder;
        }

        // Internal categories (the system's collections) follow the public ones, linked to each other by their own keys.
        var offset = package.Categories.Count;
        for (var i = 0; i < package.Collections.Count; i++)
        {
            var c = package.Collections[i];
            var row = offset + i + 2;
            sheet.Cell(row, 1).Value = c.Name;
            sheet.Cell(row, 2).Value = c.Description ?? string.Empty;
            sheet.Cell(row, 3).Value = "Active";
            sheet.Cell(row, 4).Value = "Internal";
            sheet.Cell(row, 5).Value = c.ParentName is null ? string.Empty : InternalKey(c.ParentName);
            sheet.Cell(row, 11).Value = InternalKey(c.Name);
            sheet.Cell(row, 12).Value = c.SortOrder;
        }

        Finish(sheet);
    }

    private static string InternalKey(string name) => $"INT-{CatalogSlug.From(name).ToUpperInvariant()}";

    /// <summary>Writes each product as a Parent row followed by its Variant rows; returns each variant SKU's display name.</summary>
    private static Dictionary<string, string> WriteProducts(IXLWorksheet sheet, ParsedCatalogPackage package)
    {
        var categoryBySlug = package.Categories
            .GroupBy(c => c.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        string Path(string slug)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = slug;
            while (current is not null && seen.Add(current) && categoryBySlug.TryGetValue(current, out var category))
            {
                names.Insert(0, category.NameEn);
                current = category.ParentSlug;
            }

            return names.Count == 0 ? slug : string.Join(" > ", names);
        }

        var groupsByProduct = package.OptionGroups.GroupBy(g => g.ProductImportKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortOrder).ToList(), StringComparer.OrdinalIgnoreCase);
        var variantsByProduct = package.Variants.GroupBy(v => v.ProductImportKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var groupOfValue = package.Options
            .GroupBy(o => (o.ProductImportKey.ToUpperInvariant(), o.Value.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.First().GroupKey);

        var variantNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var takenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var row = 2;
        foreach (var product in package.Products)
        {
            var parentCode = ParentCode(product.ImportKey, takenCodes);
            var groups = groupsByProduct.GetValueOrDefault(product.ImportKey) ?? [];

            sheet.Cell(row, 1).Value = parentCode;
            sheet.Cell(row, 2).Value = product.NameEn;
            sheet.Cell(row, 3).Value = product.DescriptionEn ?? string.Empty;
            sheet.Cell(row, 4).Value = product.Status ?? string.Empty;
            sheet.Cell(row, 5).Value = "Parent";
            sheet.Cell(row, 7).Value = string.Join(", ", groups.Select(g => g.Key));
            sheet.Cell(row, 8).Value = product.AdditionalDescriptionEn ?? string.Empty;
            sheet.Cell(row, 9).Value = Path(product.CategorySlug);
            sheet.Cell(row, 10).Value = string.Join("; ", product.AdditionalCategorySlugs.Select(Path));
            sheet.Cell(row, 11).Value = string.Join("; ", product.CollectionNames ?? []);
            sheet.Cell(row, 12).Value = product.ImagePaths.Count > 0 ? System.IO.Path.GetFileName(product.ImagePaths[0]) : string.Empty;
            sheet.Cell(row, 13).Value = product.NameAr ?? string.Empty;
            sheet.Cell(row, 14).Value = product.DescriptionAr ?? string.Empty;
            sheet.Cell(row, 15).Value = product.AdditionalDescriptionAr ?? string.Empty;
            row++;

            foreach (var variant in variantsByProduct.GetValueOrDefault(product.ImportKey) ?? [])
            {
                var picked = variant.SelectedOptionValues
                    .Select(value => (Value: value, Group: groupOfValue.GetValueOrDefault((product.ImportKey.ToUpperInvariant(), value.ToUpperInvariant()), string.Empty)))
                    .ToList();
                var name = picked.Count == 0 ? product.NameEn : $"{product.NameEn} - {string.Join(" - ", picked.Select(p => p.Value))}";
                if (variant.Sku is not null)
                {
                    variantNames[variant.Sku] = name;
                }

                sheet.Cell(row, 1).Value = variant.Sku ?? string.Empty;
                sheet.Cell(row, 2).Value = name;
                sheet.Cell(row, 4).Value = variant.Status ?? string.Empty;
                sheet.Cell(row, 5).Value = "Variant";
                sheet.Cell(row, 6).Value = parentCode;
                sheet.Cell(row, 8).Value = variant.Description ?? string.Empty;
                sheet.Cell(row, 16).Value = string.Join("; ", picked.Where(p => p.Group.Length > 0).Select(p => $"{p.Group}={p.Value}"));
                row++;
            }
        }

        Finish(sheet);
        return variantNames;
    }

    /// <summary>The parent's SKU cell: a product has no SKU of its own (its variants do), so a short stable code stands in.</summary>
    private static string ParentCode(string importKey, HashSet<string> taken)
    {
        var plain = Guid.TryParse(importKey, out var id) ? $"P-{id.ToString("N")[..8].ToUpperInvariant()}" : importKey;
        var code = plain;
        for (var n = 2; !taken.Add(code); n++)
        {
            code = $"{plain}-{n}";
        }

        return code;
    }

    private static void WriteStock(IXLWorksheet sheet, ParsedCatalogPackage package, Dictionary<string, string> variantNames)
    {
        var row = 2;
        foreach (var group in package.Codes.GroupBy(c => (c.VariantSku, Note: c.Notes ?? string.Empty)))
        {
            foreach (var chunk in group.Chunk(MaxCodesPerCell))
            {
                sheet.Cell(row, 1).Value = group.Key.VariantSku;
                sheet.Cell(row, 2).Value = variantNames.GetValueOrDefault(group.Key.VariantSku, string.Empty);
                sheet.Cell(row, 3).Value = "PerLine";
                sheet.Cell(row, 4).Value = string.Join("\n", chunk.Select(c => c.Code));
                sheet.Cell(row, 4).Style.Alignment.WrapText = true;
                sheet.Cell(row, 5).Value = group.Key.Note;
                row++;
            }
        }

        Finish(sheet);
    }

    // ---------- shared sheet helpers ----------

    private static IXLWorksheet Header(IXLWorksheet sheet, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
        }

        sheet.SheetView.FreezeRows(1);
        return sheet;
    }

    private static void Finish(IXLWorksheet sheet) => sheet.Columns().AdjustToContents(1, 60);

    private static void PricingHeader(IXLWorksheet sheet)
    {
        (string Text, int From, int To)[] banner =
            [("PRODUCT DETAILS", 1, 2), ("COST", 3, 3), ("SELLING PRICES", 4, 6), ("MARGINS • AUTO", 7, 9), ("SALE PERIOD", 10, 11), ("SKU IDENTITY", 12, 12)];
        foreach (var (text, from, to) in banner)
        {
            var range = sheet.Range(1, from, 1, to);
            range.Merge();
            range.Value = text;
            range.Style.Font.Bold = true;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        for (var i = 0; i < PricingHeaders.Length; i++)
        {
            var cell = sheet.Cell(2, i + 1);
            cell.Value = PricingHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
            sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E7FF");
        }

        sheet.SheetView.FreezeRows(2);
    }

    private static void PricingRowFormat(IXLWorksheet sheet, int row)
    {
        // The margins are the sheet's own arithmetic (price minus cost over price) and are ignored on import.
        sheet.Cell(row, 7).FormulaA1 = $"IF(OR(C{row}=\"\",D{row}=\"\",D{row}<=0),\"\",(D{row}-C{row})/D{row})";
        sheet.Cell(row, 8).FormulaA1 = $"IF(OR(C{row}=\"\",E{row}=\"\",E{row}<=0),\"\",(E{row}-C{row})/E{row})";
        sheet.Cell(row, 9).FormulaA1 = $"IF(OR(C{row}=\"\",F{row}=\"\",F{row}<=0),\"\",(F{row}-C{row})/F{row})";
        sheet.Range(row, 3, row, 6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(row, 7, row, 9).Style.NumberFormat.Format = "0.00%";
        sheet.Range(row, 10, row, 11).Style.NumberFormat.Format = "yyyy-mm-dd";
    }

    private static void SetAmount(IXLCell cell, decimal? amount)
    {
        if (amount is { } value)
        {
            cell.Value = value;
        }
    }

    private static void Dropdown(IXLWorksheet sheet, int column, IEnumerable<string> values) =>
        sheet.Range(2, column, DropdownLastRow, column).CreateDataValidation().List(Quoted(values), true);

    // A literal list source must be a quoted string or Excel rejects the file as corrupt.
    private static string Quoted(IEnumerable<string> values) => $"\"{string.Join(",", values)}\"";

    private static void Instructions(XLWorkbook workbook, (string Column, string Required, string Description)[] notes)
    {
        var sheet = workbook.Worksheets.Add("Instructions");
        sheet.Cell(1, 1).Value = "Column";
        sheet.Cell(1, 2).Value = "Required";
        sheet.Cell(1, 3).Value = "Description";
        sheet.Range(1, 1, 1, 3).Style.Font.Bold = true;
        for (var i = 0; i < notes.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = notes[i].Column;
            sheet.Cell(i + 2, 2).Value = notes[i].Required;
            sheet.Cell(i + 2, 3).Value = notes[i].Description;
        }

        sheet.Columns().AdjustToContents(1, 120);
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ---------- the Instructions tab of each template ----------

    private static readonly (string, string, string)[] CategoryNotes =
    [
        ("NameEN", "Yes", "Category name in English."),
        ("DescriptionEN / DescriptionAR", "No", "Short text shown on the category page."),
        ("Status", "No", "Draft, Active or Inactive. Blank means Active. Draft and Inactive categories are hidden from the storefront."),
        ("CategoryType", "No", "Public (shown on the storefront) or Internal (an admin-only grouping, e.g. 'Redeem Code', kept under Internal Categories; never shown to customers). An Internal row only uses its name, description, parent and sort order."),
        ("ParentCategoryKey", "No", "The CategoryKey of the parent category row (or an existing category's slug). Blank for a top-level category."),
        ("FilterGroups", "No", "Filter groups this category adds to the ones it inherits from its parent, separated by ';' (e.g. Price;Region;ProductType)."),
        ("ExcludedFilterGroups", "No", "Inherited filter groups this category drops, separated by ';'."),
        ("Slug", "No", "URL name (lower-case letters, digits, dashes). Blank: made from NameEN. An existing slug updates that category."),
        ("NameAR", "No", "Category name in Arabic."),
        ("DescriptionAR", "No", "Arabic description."),
        ("CategoryKey", "No", "A key of your own that other rows use in ParentCategoryKey. Only needs to be unique inside this file."),
        ("SortOrder", "No", "Position among its siblings."),
    ];

    private static readonly (string, string, string)[] ProductNotes =
    [
        ("SKU", "Yes", "Parent: any code of your own that its variants name in ParentSKU. Variant: the variant's real SKU."),
        ("NameEN / NameAR", "Yes / No", "Parent: the product name. Variant: its name is built from its options, so this is only used to read them when VariantOptions is blank."),
        ("DescriptionEN / DescriptionAR", "No", "Product description (parent rows)."),
        ("Status", "No", "Draft, Active, Inactive or Archived. An import never publishes a product: an Active parent stays Draft until you publish it."),
        ("RecordType", "Yes", "Parent (the product) or Variant (one sellable option combination)."),
        ("ParentSKU", "Variants", "The SKU of the parent row this variant belongs to."),
        ("VariantGroups", "No", "Parent: the option groups, separated by commas (e.g. Duration, Region)."),
        ("AdditionalDescriptionEN / AR", "No", "Parent: extra text under the description (plan details, restrictions). Variant: text shown with that option (English only)."),
        ("PrimaryCategory", "Parents", "The category as a path of names, e.g. Xbox > Subscriptions > Game Pass. Missing categories are created."),
        ("AdditionalCategories", "No", "More category paths, separated by ';'."),
        ("InternalCategories", "No", "Internal (admin-only) category names, separated by ';'. Missing ones are created."),
        ("MainImageRef", "No", "Reference to the main image. Images are not imported from a sheet: add them on the product page."),
        ("VariantOptions", "No", "Variant: the option in each group, e.g. Duration=1 Month; Region=US. When blank the options are read from NameEN (Parent - 1 Month - US), which needs exactly one value per group."),
    ];

    private static readonly (string, string, string)[] PricingNotes =
    [
        ("Product Name", "No", "For your reference only."),
        ("Currency", "Yes", "USD, EUR, EGP or SAR. Amounts are converted to USD, the stored currency, at the current exchange rate."),
        ("Cost Price", "No", "What the variant costs you."),
        ("Normal Price", "No", "The regular selling price."),
        ("Sale Price", "No", "A temporary price below the Normal Price. Blank leaves the current sale as it is."),
        ("Elite Price", "No", "The price ELITE members pay."),
        ("Margin % columns", "No", "Calculated by the sheet. Ignored on import."),
        ("Sale Start / Sale End", "No", "The days the Sale Price applies (both included). Leave blank for an open-ended sale; set Sale End to a past date to end it."),
        ("SKU", "Yes", "The variant's SKU. A blank cell means 'leave that price as it is'."),
    ];

    private static readonly (string, string, string)[] StockNotes =
    [
        ("SKU", "Yes", "The variant's SKU."),
        ("ProductName", "No", "For your reference only."),
        ("EntryMode", "No", "PerLine: every line of StockData is one code. WholeEntry: the whole cell is one code (e.g. a multi-line account). Blank means PerLine."),
        ("StockData", "Yes", "The code(s)."),
        ("InternalNote", "No", "A note kept with the codes. Never shown to customers."),
    ];
}
