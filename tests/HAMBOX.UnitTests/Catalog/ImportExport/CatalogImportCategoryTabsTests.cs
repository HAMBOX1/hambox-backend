using ClosedXML.Excel;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Infrastructure.Packaging;

namespace HAMBOX.UnitTests.Catalog.ImportExport;

/// <summary>
/// A Products workbook may carry one tab per category (the tab name is the category) next to the classic
/// "Products" tab; a plain single-tab workbook must keep reading exactly as before.
/// </summary>
public sealed class CatalogImportCategoryTabsTests
{
    private static readonly PackageCryptoService Crypto = new();

    private static async Task<ParsedCatalogPackage> ParseAsync(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var parser = new CatalogImportParser(new HamboxPackageReader(Crypto));
        return await parser.ParseAsync(stream, CatalogPackageFormat.Xlsx, CatalogImportEntityType.Products, null, CancellationToken.None);
    }

    private static IXLWorksheet AddSheet(XLWorkbook workbook, string name, string[] headers, params string[][] rows)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];
            }
        }

        return sheet;
    }

    [Fact]
    public async Task EachExtraTab_IsACategory_AndRowNumbersStayUnique()
    {
        using var workbook = new XLWorkbook();
        string[] headers = ["NameEn", "Price", "CategorySlug"];
        AddSheet(workbook, "Products", headers, ["Loose Product", "5", "gift-cards"]);
        AddSheet(workbook, "Xbox", headers, ["Game Pass 1M", "9.99", ""], ["Forza", "59.99", ""]);
        AddSheet(workbook, "Playstation", headers, ["PSN 10", "10", ""]);

        var package = await ParseAsync(workbook);

        Assert.Equal(4, package.Products.Count);
        Assert.Equal("gift-cards", package.Products.Single(p => p.NameEn == "Loose Product").CategorySlug);
        Assert.Equal("Xbox", package.Products.Single(p => p.NameEn == "Forza").CategorySlug);
        Assert.Equal("Playstation", package.Products.Single(p => p.NameEn == "PSN 10").CategorySlug);
        Assert.Equal(package.Products.Count, package.Products.Select(p => p.RowNumber).Distinct().Count());
    }

    [Fact]
    public async Task AFilledCategoryCell_WinsOverTheTabName()
    {
        using var workbook = new XLWorkbook();
        string[] headers = ["NameEn", "CategorySlug"];
        AddSheet(workbook, "Xbox", headers, ["Game Pass 1M", "game-pass"]);
        AddSheet(workbook, "Playstation", headers, ["PSN 10", ""]);

        var package = await ParseAsync(workbook);

        Assert.Equal("game-pass", package.Products.Single(p => p.NameEn == "Game Pass 1M").CategorySlug);
        Assert.Equal("Playstation", package.Products.Single(p => p.NameEn == "PSN 10").CategorySlug);
    }

    [Fact]
    public async Task ReservedTabs_AreNeverReadAsCategories()
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Products", ["NameEn", "CategorySlug"], ["Steam 20", "games"]);
        AddSheet(workbook, "Variant Groups", ["ProductImportKey", "GroupKey", "DisplayName"], ["Steam 20", "region", "Region"]);
        AddSheet(workbook, "Instructions", ["Sheet", "Column"], ["Products", "NameEn"]);
        AddSheet(workbook, "Lists", ["Value"], ["Active"]);

        var package = await ParseAsync(workbook);

        Assert.Single(package.Products);
        Assert.Single(package.OptionGroups);
    }

    [Fact]
    public async Task ASingleTabWorkbook_ReadsAsProducts_WhateverItIsCalled()
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Sheet1", ["NameEn", "CategorySlug"], ["Steam 20", "games"]);

        var package = await ParseAsync(workbook);

        Assert.Equal("games", Assert.Single(package.Products).CategorySlug);
    }
}
