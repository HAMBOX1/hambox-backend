using ClosedXML.Excel;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportLookups;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Infrastructure.BackgroundJobs;
using HAMBOX.Modules.Catalog.Infrastructure.Packaging;

namespace HAMBOX.UnitTests.Catalog.ImportExport;

/// <summary>
/// The downloadable template and the Excel export both carry the Products tab, one tab per category and the Variant Groups /
/// Variant Options tabs — and the export reads back through the same import parser.
/// </summary>
public sealed class CatalogWorkbookTabsTests
{
    private static CatalogImportLookups Lookups(params string[] categories) => new(
        categories.Select(c => new CatalogImportLookupItem(c.ToLowerInvariant(), c)).ToList(),
        [], [], ["Draft", "Active"], ["Draft", "Active"], ["USD"], [], []);

    private static string[] SheetNames(byte[] file)
    {
        using var workbook = new XLWorkbook(new MemoryStream(file));
        return workbook.Worksheets.Select(s => s.Name).ToArray();
    }

    [Fact]
    public void TheTemplate_HasAProductsTab_ACategoryTabEach_AndTheVariantTabs()
    {
        var file = new CatalogImportTemplateGenerator().Generate(CatalogImportEntityType.Products, Lookups("Xbox", "Playstation", "Gift Cards"));

        var names = SheetNames(file);

        Assert.Contains("Products", names);
        Assert.Contains("Xbox", names);
        Assert.Contains("Playstation", names);
        Assert.Contains("Gift Cards", names);
        Assert.Contains("Variant Groups", names);
        Assert.Contains("Variant Options", names);
        Assert.Contains("Instructions", names);
        // The category tabs come right after Products.
        Assert.True(Array.IndexOf(names, "Xbox") > Array.IndexOf(names, "Products"));
        Assert.True(Array.IndexOf(names, "Xbox") < Array.IndexOf(names, "Variant Groups"));
    }

    [Fact]
    public void ACategoryThatCannotBeATabName_IsLeftOut_AndReservedNamesNeverBecomeTabs()
    {
        var file = new CatalogImportTemplateGenerator().Generate(
            CatalogImportEntityType.Products,
            Lookups("Xbox", "Games / PC", new string('A', 40), "Instructions", "xbox"));

        var names = SheetNames(file);

        Assert.Contains("Xbox", names);
        Assert.DoesNotContain("Games / PC", names);
        Assert.DoesNotContain(new string('A', 40), names);
        Assert.Single(names, n => n == "Instructions");
        Assert.Single(names, n => string.Equals(n, "Xbox", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheOtherEntityTemplates_StayASingleSheet()
    {
        var file = new CatalogImportTemplateGenerator().Generate(CatalogImportEntityType.Categories, Lookups("Xbox"));

        Assert.DoesNotContain("Xbox", SheetNames(file));
    }

    [Fact]
    public async Task TheExport_PutsEachProductOnItsCategoryTab_AndReadsBackWithoutDuplicates()
    {
        ParsedProductRow Product(int n, string name, string categorySlug) => new(
            n, $"key-{n}", name, null, "d", null, 10m, categorySlug, "Active", 5, [], []);

        var package = ParsedCatalogPackage.Empty with
        {
            Categories =
            [
                new ParsedCategoryRow(1, "xbox", "Xbox", null, null, true, 0),
                new ParsedCategoryRow(2, "psn", "Playstation", null, null, true, 1),
                new ParsedCategoryRow(3, "bad", "Games / PC", null, null, true, 2),
            ],
            Products =
            [
                Product(1, "Game Pass", "xbox"),
                Product(2, "Forza", "xbox"),
                Product(3, "PSN 10", "psn"),
                Product(4, "PC Game", "bad"),
                Product(5, "Loose", ""),
            ],
            OptionGroups = [new ParsedOptionGroupRow("key-1", "duration", "Duration", null, 0, true)],
            Options = [new ParsedOptionRow("key-1", "duration", "1m", "1 Month", 0)],
        };

        var file = ExportCatalogJobHandler.BuildCatalogXlsx(package);

        var names = SheetNames(file);
        Assert.Equal(["Products", "Playstation", "Xbox", "Variant Groups", "Variant Options", "Instructions"], names);

        using var stream = new MemoryStream(file);
        var parser = new CatalogImportParser(new HamboxPackageReader(new PackageCryptoService()));
        var back = await parser.ParseAsync(stream, CatalogPackageFormat.Xlsx, CatalogImportEntityType.Products, null, CancellationToken.None);

        Assert.Equal(5, back.Products.Count);
        Assert.Equal(5, back.Products.Select(p => p.NameEn).Distinct().Count());
        Assert.Single(back.OptionGroups);
        Assert.Single(back.Options);
        Assert.Equal("xbox", back.Products.Single(p => p.NameEn == "Forza").CategorySlug);
    }
}
