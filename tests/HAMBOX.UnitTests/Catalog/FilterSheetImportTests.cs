using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.Filters;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.Packaging;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog;

/// <summary>Bulk filter management from Excel / CSV sheets: validation first, data protection, and undo.</summary>
public sealed class FilterSheetImportTests
{
    private static async Task<(TestCatalogDbContext Db, List<Product> Products)> SeedAsync(int count = 3)
    {
        var db = TestCatalogDbContextFactory.Create();
        var category = Category.Create("أ", "Xbox Games", $"xbox-{Guid.NewGuid():N}");
        db.Categories.Add(category);
        var products = Enumerable.Range(1, count)
            .Select(i => Product.Create($"منتج {i}", $"Product {i}", "d", "d", 10m * i, category.Id))
            .ToList();
        db.Products.AddRange(products);
        for (var i = 0; i < products.Count; i++)
        {
            var variant = ProductVariant.Create(products[i].Id, $"SKU-{i + 1}");
            variant.Activate();
            db.ProductVariants.Add(variant);
        }

        await db.SaveChangesAsync();
        var save = new SaveFilterDefinitionCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin"));
        await save.Handle(new SaveFilterDefinitionCommand(null, new SaveFilterDefinitionInput(
            "Game Mode", null, true, true, false, true, null,
            [new FilterValueDto(null, "", "Single Player", null, 0, true), new FilterValueDto(null, "", "Multiplayer", null, 1, true)])), CancellationToken.None);
        await save.Handle(new SaveFilterDefinitionCommand(null, new SaveFilterDefinitionInput(
            "Region", null, false, true, false, true, null,
            [new FilterValueDto(null, "", "Global", null, 0, true), new FilterValueDto(null, "", "US", null, 1, true)])), CancellationToken.None);
        return (db, products);
    }

    private static FilterSheet Sheet(string[] header, params string[][] rows) => new(header, rows.Select(r => (IReadOnlyList<string>)r).ToList());

    private static Task<HAMBOX.SharedKernel.Results.Result<FilterImportPreviewDto>> RunAsync(
        TestCatalogDbContext db, FilterSheet sheet, FilterAssignMode mode = FilterAssignMode.Merge, bool apply = false, bool allowNew = false, string hash = "h1") =>
        new ImportProductFiltersCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin"))
            .Handle(new ImportProductFiltersCommand(sheet, "sheet.xlsx", hash, mode, allowNew, apply), CancellationToken.None);

    [Fact]
    public async Task SeveralSkusOfOneProduct_AddUpTheirValues_AndCommasSeparateThem()
    {
        var (db, products) = await SeedAsync(1);
        var variant = ProductVariant.Create(products[0].Id, "SKU-1B");
        variant.Activate();
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        // The client's layout: the parent row carries the genres, a variant row carries its region.
        var sheet = Sheet(
            ["SKU", "ProductName", "Game Mode", "Region"],
            ["SKU-1", "Product 1", "Single Player, Multiplayer", ""],
            ["SKU-1B", "Product 1 US", "", "US"]);

        var result = (await RunAsync(db, sheet, apply: true)).Value;

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.True(result.Applied);
        Assert.Equal(1, result.ProductsToChange);
        Assert.Equal(3, await db.ProductFilterValues.CountAsync());
    }

    [Fact]
    public async Task APreviewChangesNothing_AndShowsOldAndNewValues()
    {
        var (db, products) = await SeedAsync();
        var sheet = Sheet(["ProductId", "game-mode"], [products[0].Id.ToString(), "Single Player|Multiplayer"]);

        var preview = (await RunAsync(db, sheet)).Value;

        Assert.Empty(preview.Errors);
        Assert.Equal(1, preview.ProductsToChange);
        Assert.Equal(2, preview.ValuesToAdd);
        Assert.False(preview.Applied);
        Assert.Equal("Single Player | Multiplayer".Split(" | ").Order(), preview.Changes[0].NewValues.Split(" | ").Order());
        Assert.Equal(0, await db.ProductFilterValues.CountAsync());
    }

    [Fact]
    public async Task ApplyingTagsProductsByIdOrSku_AndLeavesEverythingElseAlone()
    {
        var (db, products) = await SeedAsync();
        var priceBefore = products.Select(p => p.Price).ToList();
        var sheet = Sheet(
            ["ProductId", "SKU", "Region", "Game Mode"],
            [products[0].Id.ToString(), "", "US", "Multiplayer"],
            ["", "sku-2", "Global", ""]);

        var result = (await RunAsync(db, sheet, apply: true)).Value;

        Assert.True(result.Applied);
        Assert.Equal(2, result.ProductsToChange);
        Assert.Equal(3, await db.ProductFilterValues.CountAsync());
        Assert.Equal(priceBefore, (await db.Products.OrderBy(p => p.NameEn).ToListAsync()).Select(p => p.Price).ToList());
    }

    [Fact]
    public async Task ModesMergeReplaceAndRemoveBehaveAsNamed_AndColumnsNotInTheSheetStayUntouched()
    {
        var (db, products) = await SeedAsync(1);
        var id = products[0].Id.ToString();
        await RunAsync(db, Sheet(["ProductId", "Game Mode", "Region"], [id, "Single Player", "US"]), apply: true, hash: "a");

        await RunAsync(db, Sheet(["ProductId", "Game Mode"], [id, "Multiplayer"]), FilterAssignMode.Merge, apply: true, hash: "b");
        Assert.Equal(3, await db.ProductFilterValues.CountAsync());

        await RunAsync(db, Sheet(["ProductId", "Game Mode"], [id, "Multiplayer"]), FilterAssignMode.Replace, apply: true, hash: "c");
        Assert.Equal(2, await db.ProductFilterValues.CountAsync());

        await RunAsync(db, Sheet(["ProductId", "Game Mode"], [id, "Multiplayer"]), FilterAssignMode.Remove, apply: true, hash: "d");
        // Region was never in those sheets, so its tag is still there.
        var left = await db.ProductFilterValues.Join(db.FilterValues, t => t.FilterValueId, v => v.Id, (t, v) => v.Value).ToListAsync();
        Assert.Equal(["us"], left);
    }

    [Fact]
    public async Task UnknownProductsValuesAndRepeatedRowsAreReported_AndNothingIsApplied()
    {
        var (db, products) = await SeedAsync(1);
        var id = products[0].Id.ToString();
        var sheet = Sheet(
            ["ProductId", "Game Mode"],
            [Guid.NewGuid().ToString(), "Single Player"],
            [id, "Co-op"],
            [id, "Single Player"]);

        var result = (await RunAsync(db, sheet, apply: true)).Value;

        Assert.False(result.Applied);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Row == 2 && e.Message.Contains("No product"));
        Assert.Contains(result.Errors, e => e.Row == 3 && e.Message.Contains("Co-op"));
        Assert.Contains(result.Errors, e => e.Row == 4 && e.Message.Contains("more than once"));
        Assert.Equal(0, await db.ProductFilterValues.CountAsync());
    }

    [Fact]
    public async Task NewValuesNeedExplicitPermission_AndAreCreatedOnlyWhenAllowed()
    {
        var (db, products) = await SeedAsync(1);
        var sheet = Sheet(["ProductId", "Game Mode"], [products[0].Id.ToString(), "Co-op"]);

        var refused = (await RunAsync(db, sheet, apply: true)).Value;
        Assert.NotEmpty(refused.Errors);

        var allowed = (await RunAsync(db, sheet, apply: true, allowNew: true, hash: "new")).Value;
        Assert.True(allowed.Applied);
        Assert.Equal(["Game Mode: Co-op"], allowed.NewValues);
        Assert.Contains(await db.FilterValues.ToListAsync(), v => v.Value == "co-op");
    }

    [Fact]
    public async Task ASingleChoiceFilterRefusesSeveralValues()
    {
        var (db, products) = await SeedAsync(1);

        var result = (await RunAsync(db, Sheet(["ProductId", "Region"], [products[0].Id.ToString(), "Global|US"]))).Value;

        Assert.Contains(result.Errors, e => e.Message.Contains("allows one value"));
    }

    [Fact]
    public async Task TheSameFileCannotBeAppliedTwice_AndAnImportCanBeUndone()
    {
        var (db, products) = await SeedAsync(1);
        var sheet = Sheet(["ProductId", "Game Mode"], [products[0].Id.ToString(), "Single Player"]);

        var first = (await RunAsync(db, sheet, apply: true, hash: "same")).Value;
        var second = (await RunAsync(db, sheet, apply: true, hash: "same")).Value;
        Assert.True(first.Applied);
        Assert.False(second.Applied);
        Assert.Contains(second.Errors, e => e.Message.Contains("already applied"));

        var undo = await new UndoFilterImportCommandHandler(db).Handle(new UndoFilterImportCommand(first.LogId!.Value), CancellationToken.None);
        Assert.True(undo.IsSuccess);
        Assert.Equal(0, await db.ProductFilterValues.CountAsync());

        var again = (await RunAsync(db, sheet, apply: true, hash: "same")).Value;
        Assert.True(again.Applied);
    }

    [Fact]
    public async Task ADefinitionsSheetCreatesAndUpdatesButNeverDeletes()
    {
        var (db, _) = await SeedAsync(0);
        var sheet = Sheet(
            ["FilterKey", "FilterName", "MultiSelect", "ValueLabel"],
            ["game-mode", "Game Mode", "yes", "Co-op"],
            ["offers", "Offers", "yes", "Flash Sale"],
            ["offers", "Offers", "yes", "Black Friday"]);

        var result = await new ImportFilterDefinitionsCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin"))
            .Handle(new ImportFilterDefinitionsCommand(sheet, "defs.csv", "defs", true), CancellationToken.None);

        Assert.True(result.Value.Applied);
        Assert.Equal(3, await db.FilterValues.CountAsync(v => v.Value == "co-op" || v.Value == "flash-sale" || v.Value == "black-friday"));
        // The two values game-mode already had are still there.
        Assert.Equal(3, await db.FilterValues.CountAsync(v => v.Value == "single-player" || v.Value == "multiplayer" || v.Value == "co-op"));
        Assert.Equal(3, await db.FilterDefinitions.CountAsync());
    }

    [Fact]
    public async Task TheExportListsProductsWithTheirValues_AndTheTemplateIsEmpty()
    {
        var (db, products) = await SeedAsync(2);
        await RunAsync(db, Sheet(["ProductId", "Game Mode"], [products[0].Id.ToString(), "Single Player|Multiplayer"]), apply: true);

        var sheet = (await new ExportProductFiltersQueryHandler(db).Handle(new ExportProductFiltersQuery(null, null, false), CancellationToken.None)).Value;
        var template = (await new ExportProductFiltersQueryHandler(db).Handle(new ExportProductFiltersQuery(null, null, true), CancellationToken.None)).Value;

        Assert.Equal(["ProductId", "SKU", "Name", "Category", "game-mode", "region"], sheet.Header);
        Assert.Equal(2, sheet.Rows.Count);
        var tagged = sheet.Rows.Single(r => r[0] == products[0].Id.ToString());
        Assert.Equal("multiplayer|single-player", tagged[4]);
        Assert.Empty(template.Rows);
    }

    [Fact]
    public void CsvParsingHandlesQuotesAndSemicolons()
    {
        var records = FilterSheetCodec.ParseCsv("ProductId,Name\r\n1,\"Halo, \"\"Infinite\"\"\"\r\n2,Forza\r\n");

        Assert.Equal(3, records.Count);
        Assert.Equal("Halo, \"Infinite\"", records[1][1]);
        Assert.Equal(["a", "b"], FilterSheetCodec.ParseCsv("a;b\n")[0]);
    }
}
