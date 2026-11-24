using ClosedXML.Excel;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetImportLookups;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.BackgroundJobs;
using HAMBOX.Modules.Catalog.Infrastructure.Packaging;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

namespace HAMBOX.UnitTests.Catalog.ImportExport;

/// <summary>
/// The client's five Excel layouts (HX_Categories, HX_Products, HX_Pricing, HX_Stock): the templates carry their headers, the
/// parser reads them, the export writes them back, and the validation treats prices and categories the way the sheets mean.
/// </summary>
public sealed class ClientSheetsTests
{
    private static readonly CatalogImportParser Parser = new(new HamboxPackageReader(new PackageCryptoService()));

    private static CatalogImportLookups Lookups() => new([], [], [], ["Draft", "Active"], ["Draft", "Active"], ["USD", "SAR"], [], []);

    private static byte[] Workbook(string sheetName, string[] headers, IEnumerable<object?[]> rows, int headerRow = 1, Action<IXLWorksheet>? extra = null)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(headerRow, c + 1).Value = headers[c];
        }

        var r = headerRow + 1;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                switch (row[c])
                {
                    case null:
                        break;
                    case DateTime date:
                        sheet.Cell(r, c + 1).Value = date;
                        break;
                    default:
                        sheet.Cell(r, c + 1).Value = row[c]!.ToString();
                        break;
                }
            }

            r++;
        }

        extra?.Invoke(sheet);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static Task<ParsedCatalogPackage> ParseAsync(byte[] file, CatalogImportEntityType type) =>
        Parser.ParseAsync(new MemoryStream(file), CatalogPackageFormat.Xlsx, type, null, CancellationToken.None);

    private static string[] Headers(byte[] file, string sheet, int row = 1)
    {
        using var workbook = new XLWorkbook(new MemoryStream(file));
        var worksheet = workbook.Worksheet(sheet);
        return worksheet.Row(row).CellsUsed().Select(c => c.GetString()).ToArray();
    }

    // ---------- templates ----------

    [Theory]
    [InlineData(CatalogImportEntityType.Categories, "Categories")]
    [InlineData(CatalogImportEntityType.Products, "Products")]
    [InlineData(CatalogImportEntityType.Codes, "Stock")]
    public void TheTemplates_CarryTheClientsHeaders(CatalogImportEntityType type, string sheet)
    {
        var file = new CatalogImportTemplateGenerator().Generate(type, Lookups());

        var expected = type switch
        {
            CatalogImportEntityType.Categories => ClientSheetsWriter.CategoryHeaders,
            CatalogImportEntityType.Products => ClientSheetsWriter.ProductHeaders,
            _ => ClientSheetsWriter.StockHeaders,
        };
        Assert.Equal(expected, Headers(file, sheet));
        using var workbook = new XLWorkbook(new MemoryStream(file));
        Assert.Contains(workbook.Worksheets, s => s.Name == "Instructions");
    }

    [Fact]
    public void ThePricingTemplate_HasTheBannerRow_TheHeaderRow_AndTheMarginFormulas()
    {
        var file = new CatalogImportTemplateGenerator().Generate(CatalogImportEntityType.Pricing, Lookups());

        Assert.Equal(ClientSheetsWriter.PricingHeaders, Headers(file, "Pricing", row: 2));
        using var workbook = new XLWorkbook(new MemoryStream(file));
        var sheet = workbook.Worksheet("Pricing");
        Assert.Equal("PRODUCT DETAILS", sheet.Cell(1, 1).GetString());
        Assert.Equal("USD", sheet.Cell(3, 2).GetString());
        Assert.Contains("(D3-C3)/D3", sheet.Cell(3, 7).FormulaA1);
    }

    [Fact]
    public void TheOtherTemplates_AreUnchanged()
    {
        var file = new CatalogImportTemplateGenerator().Generate(CatalogImportEntityType.Collections, Lookups());

        using var workbook = new XLWorkbook(new MemoryStream(file));
        Assert.DoesNotContain(workbook.Worksheets, s => s.Name == "Categories");
    }

    // ---------- Categories ----------

    [Fact]
    public async Task Categories_KeysLinkParents_InternalIsKept_AndABlankSlugIsMadeFromTheName()
    {
        var file = Workbook("Categories", ClientSheetsWriter.CategoryHeaders,
        [
            ["Xbox", "Xbox products", "Draft", "Public", null, "Price;Region;ProductType", null, "xbox", "إكس بوكس", "وصف", "CAT-XBOX", "1"],
            ["Game Pass", "Passes", "Draft", "Public", "CAT-XBOX", null, "ProductType", "game-pass", null, null, "CAT-GP", null],
            ["Redeem Code", "Internal only", "Active", "Internal", null, null, null, null, null, null, "INT-REDEEM", null],
        ]);

        var package = await ParseAsync(file, CatalogImportEntityType.Categories);

        Assert.Equal(2, package.Categories.Count);
        var xbox = package.Categories[0];
        Assert.Equal(["Price", "Region", "ProductType"], xbox.FilterGroups);
        Assert.False(xbox.IsActive);
        Assert.Equal("Draft", xbox.Status);
        Assert.Equal("xbox", package.Categories[1].ParentSlug);
        Assert.Equal(["ProductType"], package.Categories[1].ExcludedFilterGroups);
        // An Internal row is one of the admin-only internal categories (collections), not a storefront category.
        var internalRow = Assert.Single(package.Collections);
        Assert.Equal("Redeem Code", internalRow.Name);
        Assert.Equal("Internal only", internalRow.Description);
        Assert.Null(internalRow.ParentName);
    }

    [Fact]
    public async Task Categories_AnInternalChildPointsAtItsInternalParentByName()
    {
        var file = Workbook("Categories", ClientSheetsWriter.CategoryHeaders,
        [
            ["Redeem", null, null, "Internal", null, null, null, null, null, null, "INT-R", "1"],
            ["Codes", null, null, "Internal", "INT-R", null, null, null, null, null, "INT-C", "2"],
        ]);

        var package = await ParseAsync(file, CatalogImportEntityType.Categories);

        Assert.Empty(package.Categories);
        Assert.Equal([null, "Redeem"], package.Collections.Select(c => c.ParentName));
    }

    [Fact]
    public async Task AnOlderCategoriesSheet_StillReadsWithoutTheClientColumns()
    {
        var file = Workbook("Categories", ["Slug", "NameEn", "NameAr", "ParentSlug", "IsActive", "SortOrder"], [["games", "Games", null, null, "true", "2"]]);

        var package = await ParseAsync(file, CatalogImportEntityType.Categories);

        Assert.Equal("games", Assert.Single(package.Categories).Slug);
        Assert.Null(package.Categories[0].Status);
    }

    // ---------- Products ----------

    private static readonly string[] ProductHeaders = ClientSheetsWriter.ProductHeaders;

    [Fact]
    public async Task Products_ParentAndVariantRows_BecomeAProductItsGroupsOptionsAndVariants()
    {
        var file = Workbook("Products", ProductHeaders,
        [
            ["GP", "Game Pass", "Plan", "Draft", "Parent", null, "Duration, Region", "Plan details", "Xbox > Subscriptions > Game Pass", "Xbox > Gifts", "Redeem Code", "gp.png", "جيم باس", null, "تفاصيل", null],
            ["GP-1M-US", "Game Pass - 1 Month - US", null, "Draft", "Variant", "GP", null, "One month", null, null, null, null, null, null, null, null],
            ["GP-3M-TR", "Game Pass - 3 Months - Turkey", null, "Draft", "Variant", "GP", null, null, null, null, null, null, null, null, null, null],
        ]);

        var package = await ParseAsync(file, CatalogImportEntityType.Products);

        var product = Assert.Single(package.Products);
        Assert.Equal("GP", product.ImportKey);
        Assert.Equal("Xbox > Subscriptions > Game Pass", product.CategorySlug);
        Assert.Equal(["Xbox > Gifts"], product.AdditionalCategorySlugs);
        Assert.Equal(["Redeem Code"], product.CollectionNames);
        Assert.Equal("Plan details", product.AdditionalDescriptionEn);
        Assert.Equal("gp.png", product.MainImageRef);
        Assert.Equal(["Duration", "Region"], package.OptionGroups.Select(g => g.Key));
        Assert.Equal(["1 Month", "US", "3 Months", "Turkey"], package.Options.Select(o => o.Value));
        Assert.Equal(["1 Month", "US"], package.Variants[0].SelectedOptionValues);
        Assert.Equal("One month", package.Variants[0].Description);
        Assert.All(package.Variants, v => Assert.Equal("GP", v.ProductImportKey));
    }

    [Fact]
    public async Task Products_TheVariantOptionsColumn_WinsOverTheName_AndANameThatDoesNotFitReadsNoOptions()
    {
        var file = Workbook("Products", ProductHeaders,
        [
            ["W", "Steam Wallet Gift Card", null, null, "Parent", null, "Denomination, Currency, Region", null, "Steam", null, null, null, null, null, null, null],
            ["W-10", "Steam Wallet - 10 USD - US", null, null, "Variant", "W", null, null, null, null, null, null, null, null, null, null],
            ["W-20", "anything", null, null, "Variant", "W", null, null, null, null, null, null, null, null, null, "Denomination=20; Currency=EUR; Region=EU"],
        ]);

        var package = await ParseAsync(file, CatalogImportEntityType.Products);

        Assert.Empty(package.Variants[0].SelectedOptionValues);
        Assert.Equal(["20", "EUR", "EU"], package.Variants[1].SelectedOptionValues);
    }

    // ---------- Pricing ----------

    [Fact]
    public async Task Pricing_IsReadPastTheBannerRow_WithDatesAndAmounts_AndBlankRowsAreSkipped()
    {
        var file = Workbook("Pricing", ClientSheetsWriter.PricingHeaders,
        [
            ["Game Pass", "SAR", "10", "20", "15", "12.5", "", "", "", new DateTime(2026, 12, 1), new DateTime(2026, 12, 31), "GP-1M-US"],
            [null, "USD", null, null, null, null, null, null, null, null, null, null],
        ], headerRow: 2, extra: sheet => sheet.Cell(1, 1).Value = "PRODUCT DETAILS");

        var package = await ParseAsync(file, CatalogImportEntityType.Pricing);

        var price = Assert.Single(package.Prices!);
        Assert.Equal("GP-1M-US", price.Sku);
        Assert.Equal("SAR", price.Currency);
        Assert.Equal((10m, 20m, 15m, 12.5m), (price.CostPrice!.Value, price.NormalPrice!.Value, price.SalePrice!.Value, price.ElitePrice!.Value));
        Assert.Equal(new DateTime(2026, 12, 1), price.SaleStart!.Value.DateTime);
        Assert.Equal(new DateTime(2026, 12, 31, 23, 59, 59), price.SaleEnd!.Value.DateTime);
        Assert.Null(price.ParseError);
    }

    [Fact]
    public async Task Pricing_ANumberOrDateThatCannotBeRead_IsReportedNotIgnored()
    {
        var file = Workbook("Pricing", ClientSheetsWriter.PricingHeaders,
            [["x", "USD", "abc", "20", null, null, null, null, null, "tomorrow", null, "S1"]], headerRow: 2);

        var price = Assert.Single((await ParseAsync(file, CatalogImportEntityType.Pricing)).Prices!);

        Assert.Contains("'abc' is not a number", price.ParseError);
        Assert.Contains("not a date", price.ParseError);
    }

    // ---------- Stock ----------

    [Fact]
    public async Task Stock_PerLineMakesACodeOfEveryLine_WholeEntryKeepsTheCellWhole()
    {
        var file = Workbook("Stock", ClientSheetsWriter.StockHeaders,
        [
            ["S1", "Steam", "PerLine", "AAA\nBBB\r\n\nCCC", "batch note"],
            ["S2", "Account", "WholeEntry", "user:pass\nextra", null],
            [null, null, "PerLine", null, null],
        ]);

        var package = await ParseAsync(file, CatalogImportEntityType.Codes);

        Assert.Equal(["AAA", "BBB", "CCC", "user:pass\nextra"], package.Codes.Select(c => c.Code));
        Assert.Equal(["S1", "S1", "S1", "S2"], package.Codes.Select(c => c.VariantSku));
        Assert.Equal("batch note", package.Codes[0].Notes);
        Assert.Equal(package.Codes.Count, package.Codes.Select(c => c.RowNumber).Distinct().Count());
    }

    // ---------- export ----------

    [Fact]
    public async Task TheExport_ReadsBackThroughTheSameSheets()
    {
        var package = ParsedCatalogPackage.Empty with
        {
            Categories =
            [
                new ParsedCategoryRow(1, "xbox", "Xbox", "إكس بوكس", null, true, 1, "Active", "Xbox stuff", null, ["Price", "Region"], []),
                new ParsedCategoryRow(2, "game-pass", "Game Pass", null, "xbox", false, 2, "Draft", null, null, [], ["Region"]),
            ],
            Collections =
            [
                new ParsedCollectionRow(1, "Redeem Code", "Internal only", null, null, null, 0),
                new ParsedCollectionRow(2, "Codes", null, null, null, "Redeem Code", 1),
            ],
            Products =
            [
                new ParsedProductRow(1, Guid.NewGuid().ToString(), "Game Pass", null, "Plan", null, 0m, "game-pass", "Draft", 0, ["products/x/a.png"], [], ["Redeem Code"], "Plan details"),
            ],
            OptionGroups = [new ParsedOptionGroupRow("PRODUCT", "Duration", "Duration", null, 0, true)],
            Options = [new ParsedOptionRow("PRODUCT", "Duration", "1 Month", "1 Month", 0)],
            Variants = [new ParsedVariantRow(1, "GP-1M", "PRODUCT", 20m, null, "Draft", 5, null, ["1 Month"], "<p>One month</p>")],
            Prices = [new ParsedPriceRow(1, "GP-1M", "USD", 10m, 20m, 15m, 12m, new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), null)],
            Codes = [new ParsedCodeRow(1, "GP-1M", "AAA", null, null, null, "USD", null, null, "note"), new ParsedCodeRow(2, "GP-1M", "BBB", null, null, null, "USD", null, null, "note")],
        };
        var key = package.Products[0].ImportKey;
        package = package with
        {
            OptionGroups = [package.OptionGroups[0] with { ProductImportKey = key }],
            Options = [package.Options[0] with { ProductImportKey = key }],
            Variants = [package.Variants[0] with { ProductImportKey = key }],
        };

        var file = ExportCatalogJobHandler.BuildCatalogXlsx(package);

        var parsed = await ParseAsync(file, CatalogImportEntityType.Categories);
        var categories = parsed.Categories;
        Assert.Equal(2, categories.Count);
        Assert.Equal("Draft", categories[1].Status);
        Assert.Equal("xbox", categories[1].ParentSlug);
        Assert.Equal(["Region"], categories[1].ExcludedFilterGroups);
        Assert.Equal(["Redeem Code", "Codes"], parsed.Collections.Select(c => c.Name));
        Assert.Equal("Redeem Code", parsed.Collections[1].ParentName);

        var products = await ParseAsync(file, CatalogImportEntityType.Products);
        var product = Assert.Single(products.Products);
        Assert.Equal("Xbox > Game Pass", product.CategorySlug);
        Assert.Equal(["Redeem Code"], product.CollectionNames);
        Assert.Equal("Plan details", product.AdditionalDescriptionEn);
        Assert.Equal("a.png", product.MainImageRef);
        var variant = Assert.Single(products.Variants);
        Assert.Equal("GP-1M", variant.Sku);
        Assert.Equal(["1 Month"], variant.SelectedOptionValues);
        Assert.Equal("<p>One month</p>", variant.Description);
        Assert.Equal(product.ImportKey, variant.ProductImportKey);

        var price = Assert.Single((await ParseAsync(file, CatalogImportEntityType.Pricing)).Prices!);
        Assert.Equal((10m, 20m, 15m, 12m), (price.CostPrice!.Value, price.NormalPrice!.Value, price.SalePrice!.Value, price.ElitePrice!.Value));
        Assert.Equal(new DateTime(2026, 12, 1), price.SaleStart!.Value.DateTime);
        Assert.Null(price.SaleEnd);

        var stock = (await ParseAsync(file, CatalogImportEntityType.Codes)).Codes;
        Assert.Equal(["AAA", "BBB"], stock.Select(c => c.Code));
        Assert.All(stock, c => Assert.Equal("note", c.Notes));
    }

    [Fact]
    public async Task TheExport_LeavesOutTheStockSheet_WhenNoCodesAreIncluded()
    {
        var file = ExportCatalogJobHandler.BuildCatalogXlsx(ParsedCatalogPackage.Empty);

        using var workbook = new XLWorkbook(new MemoryStream(file));
        Assert.DoesNotContain(workbook.Worksheets, s => s.Name == "Stock");
        Assert.Empty((await ParseAsync(file, CatalogImportEntityType.Codes)).Codes);
    }

    // ---------- category paths ----------

    [Fact]
    public async Task ACategoryPath_ReusesWhatExists_AndCreatesTheMissingLevels()
    {
        var db = TestCatalogDbContextFactory.Create();
        db.Categories.Add(Category.Create("إكس", "Xbox", "xbox"));
        await db.SaveChangesAsync();
        var package = ParsedCatalogPackage.Empty with
        {
            Products =
            [
                new ParsedProductRow(1, "GP", "Game Pass", null, null, null, null, "Xbox > Subscriptions > Game Pass", null, 0, [], []),
                new ParsedProductRow(2, "GP2", "Game Pass 2", null, null, null, null, "Xbox > Subscriptions", null, 0, [], []),
            ],
        };

        var resolved = await CatalogImportLookupResolver.ResolveAsync(package, db, CancellationToken.None);

        var subscriptions = resolved.Categories.Single(c => c.NameEn == "Subscriptions");
        Assert.Equal("xbox", subscriptions.ParentSlug);
        var gamePass = resolved.Categories.Single(c => c.NameEn == "Game Pass");
        Assert.Equal(subscriptions.Slug, gamePass.ParentSlug);
        Assert.Equal(gamePass.Slug, resolved.Products[0].CategorySlug);
        Assert.Equal(subscriptions.Slug, resolved.Products[1].CategorySlug);
        Assert.Equal(2, resolved.Categories.Count);
    }

    // ---------- price validation ----------

    private static async Task<(TestCatalogDbContext Db, ProductVariant Variant)> SeedVariantAsync()
    {
        var db = TestCatalogDbContextFactory.Create();
        var category = Category.Create("أ", "Games", "games");
        db.Categories.Add(category);
        var product = Product.Create("م", "Game", "d", "d", 30m, category.Id);
        db.Products.Add(product);
        var variant = ProductVariant.Create(product.Id, "GAME-1");
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        return (db, variant);
    }

    private static ParsedPriceRow Price(string sku, string currency = "USD", decimal? normal = 20m, decimal? sale = null, decimal? cost = null, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        new(1, sku, currency, cost, normal, sale, null, start, end);

    private static async Task<CatalogImportPlanRow<ParsedPriceRow>> PlanOneAsync(params ParsedPriceRow[] rows)
    {
        var (db, _) = await SeedVariantAsync();
        var plan = await CatalogImportMatcher.BuildPlanAsync(
            ParsedCatalogPackage.Empty with { Prices = rows }, db, CatalogSkuStrategy.UseImportedSku, CancellationToken.None);
        return plan.Prices!.Last();
    }

    [Fact]
    public async Task PriceValidation_AcceptsAKnownSku_CaseInsensitively()
    {
        var row = await PlanOneAsync(Price("game-1", sale: 15m, cost: 8m));

        Assert.Equal(CatalogImportRowStatus.Updated, row.Status);
    }

    [Theory]
    [InlineData("NOPE", "USD", 20, null, "was not found")]
    [InlineData("GAME-1", "GBP", 20, null, "Currency must be")]
    [InlineData("GAME-1", "USD", 20, 25, "lower than the Normal Price")]
    [InlineData("GAME-1", "USD", 0, null, "greater than zero")]
    public async Task PriceValidation_RejectsWhatCannotBeApplied(string sku, string currency, int normal, int? sale, string message)
    {
        var row = await PlanOneAsync(Price(sku, currency, normal, sale));

        Assert.Equal(CatalogImportRowStatus.Invalid, row.Status);
        Assert.Contains(message, row.Errors.Single());
    }

    [Fact]
    public async Task PriceValidation_RejectsASaleThatEndsBeforeItStarts_AndASkuRepeatedInTheFile()
    {
        var day = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

        var backwards = await PlanOneAsync(Price("GAME-1", sale: 10m, start: day, end: day.AddDays(-1)));
        var repeated = await PlanOneAsync(Price("GAME-1"), Price("GAME-1"));

        Assert.Contains("Sale End", backwards.Errors.Single());
        Assert.Contains("more than once", repeated.Errors.Single());
    }

    [Fact]
    public async Task ApplyingPrices_ConvertsToUsd_KeepsWhatIsBlank_AndRefusesASaleAboveTheNormalPrice()
    {
        var (db, variant) = await SeedVariantAsync();
        var rates = new Dictionary<string, decimal> { ["SAR"] = 4m };
        var day = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
        async Task<CatalogPriceApplyResult> RunAsync(params ParsedPriceRow[] rows)
        {
            var plan = await CatalogImportMatcher.BuildPlanAsync(
                ParsedCatalogPackage.Empty with { Prices = rows }, db, CatalogSkuStrategy.UseImportedSku, CancellationToken.None);
            return await CatalogPriceApplier.ApplyAsync(db, plan.Prices!, rates, CancellationToken.None);
        }

        var first = await RunAsync(new ParsedPriceRow(1, "GAME-1", "SAR", 40m, 80m, 60m, 56m, day, day.AddDays(30)));

        Assert.Equal(1, first.Updated);
        Assert.Equal((10m, 20m, 15m, 14m), (variant.CostPrice!.Value, variant.PriceOverride!.Value, variant.SalePrice!.Value, variant.MemberPrice!.Value));
        Assert.Equal(day, variant.SaleStartsOn);

        var second = await RunAsync(new ParsedPriceRow(1, "GAME-1", "USD", 12m, null, null, null, null, null));

        Assert.Equal(1, second.Updated);
        Assert.Equal(12m, variant.CostPrice);
        Assert.Equal((20m, 15m, 14m), (variant.PriceOverride!.Value, variant.SalePrice!.Value, variant.MemberPrice!.Value));

        // A sale at or above the normal price already stored is refused when the row does not carry its own normal price.
        var third = await RunAsync(new ParsedPriceRow(1, "GAME-1", "USD", null, null, 25m, null, null, null));

        Assert.Equal(0, third.Updated);
        Assert.Contains("lower than the normal price", third.Errors.Single());
        Assert.Equal(15m, variant.SalePrice);

        var noRate = await RunAsync(new ParsedPriceRow(1, "GAME-1", "EUR", 1m, null, null, null, null, null));

        Assert.Contains("no exchange rate for EUR", noRate.Errors.Single());
    }

    [Fact]
    public async Task APriceRowThatChangesNothing_IsADuplicate()
    {
        var (db, variant) = await SeedVariantAsync();
        variant.SetPriceOverride(20m);
        variant.SetCostPrice(8m);
        await db.SaveChangesAsync();

        var plan = await CatalogImportMatcher.BuildPlanAsync(
            ParsedCatalogPackage.Empty with { Prices = [Price("GAME-1", normal: 20m, cost: 8m)] }, db, CatalogSkuStrategy.UseImportedSku, CancellationToken.None);

        Assert.Equal(CatalogImportRowStatus.Duplicate, plan.Prices!.Single().Status);
    }

    [Fact]
    public async Task ARepeatedCodeInTheSameFile_IsADuplicate_AndAnOptionlessVariantIsWarnedAbout()
    {
        var (db, _) = await SeedVariantAsync();
        var package = ParsedCatalogPackage.Empty with
        {
            Codes = [new ParsedCodeRow(1, "GAME-1", "AAA", null, null, null, "USD", null, null), new ParsedCodeRow(2, "GAME-1", "AAA", null, null, null, "USD", null, null)],
            Products = [new ParsedProductRow(1, "P", "P", null, null, null, null, "games", null, 0, [], [])],
            OptionGroups = [new ParsedOptionGroupRow("P", "Region", "Region", null, 0, true)],
            Variants = [new ParsedVariantRow(1, "P-1", "P", null, null, null, 5, null, [])],
        };

        var plan = await CatalogImportMatcher.BuildPlanAsync(package, db, CatalogSkuStrategy.UseImportedSku, CancellationToken.None);

        Assert.Equal([CatalogImportRowStatus.New, CatalogImportRowStatus.Duplicate], plan.Codes.Select(c => c.Status));
        Assert.Contains(plan.Warnings, w => w.Contains("P-1") && w.Contains("options"));
    }

    // ---------- domain ----------

    [Fact]
    public void ASale_AppliesOnlyInsideItsWindow_AndTheResolverUsesIt()
    {
        var product = Product.Create("م", "Game", "d", "d", 30m, Guid.NewGuid());
        var variant = ProductVariant.Create(product.Id, "G", null, 20m);
        var now = DateTimeOffset.UtcNow;

        variant.SetSale(15m, now.AddDays(1), null);
        Assert.Null(variant.ActiveSalePrice(now));
        Assert.Equal(20m, HAMBOX.Modules.Catalog.Application.Services.EffectivePriceResolver.Resolve(variant, product));

        variant.SetSale(15m, now.AddDays(-1), now.AddDays(1));
        Assert.Equal(15m, variant.ActiveSalePrice(now));
        Assert.Equal(15m, HAMBOX.Modules.Catalog.Application.Services.EffectivePriceResolver.Resolve(variant, product));
        Assert.Equal(12m, HAMBOX.Modules.Catalog.Application.Services.EffectivePriceResolver.Resolve(variant, product, 12m));

        variant.SetSale(15m, now.AddDays(-3), now.AddDays(-2));
        Assert.Null(variant.ActiveSalePrice(now));

        variant.SetSale(null, now, now);
        Assert.Null(variant.SaleStartsOn);
    }

    [Fact]
    public void ADraftCategory_IsInactive_AndActivatingItClearsTheDraft()
    {
        var category = Category.Create("أ", "Games", "games");

        category.SetDraft(true);
        Assert.True(category.IsDraft);
        Assert.False(category.IsActive);

        category.Activate();
        Assert.False(category.IsDraft);
        Assert.True(category.IsActive);
    }
}
