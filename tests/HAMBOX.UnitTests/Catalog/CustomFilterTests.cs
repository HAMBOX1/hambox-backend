using HAMBOX.Modules.Catalog.Application.Features.Filters;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductFacets;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog;

/// <summary>Filters the admin creates by hand: definitions, tagging products, and how they appear as storefront facets.</summary>
public sealed class CustomFilterTests
{
    private static SaveFilterDefinitionInput GameMode(bool multi = true, bool showWhenEmpty = false, IReadOnlyList<Guid>? scope = null) => new(
        "Game Mode", "نمط اللعب", multi, true, showWhenEmpty, true, scope,
        [
            new FilterValueDto(null, "", "Single Player", "فردي", 0, true),
            new FilterValueDto(null, "", "Multiplayer", "جماعي", 1, true),
        ]);

    private static async Task<(TestCatalogDbContext Db, List<Product> Products)> SeedAsync(int count = 3)
    {
        var db = TestCatalogDbContextFactory.Create();
        var category = Category.Create("أ", "Xbox Games", $"xbox-{Guid.NewGuid():N}");
        db.Categories.Add(category);
        var products = Enumerable.Range(1, count)
            .Select(i => Product.Create($"منتج {i}", $"Product {i}", "d", "d", 10m * i, category.Id))
            .ToList();
        db.Products.AddRange(products);
        foreach (var product in products)
        {
            product.Activate();
            var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}"[..20]);
            variant.Activate();
            db.ProductVariants.Add(variant);
        }

        await db.SaveChangesAsync();
        return (db, products);
    }

    private static async Task<FilterDefinitionDto> CreateAsync(TestCatalogDbContext db, SaveFilterDefinitionInput input)
    {
        var result = await new SaveFilterDefinitionCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin"))
            .Handle(new SaveFilterDefinitionCommand(null, input), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        return result.Value;
    }

    private static Task<HAMBOX.SharedKernel.Results.Result<AssignFilterValuesResultDto>> AssignAsync(
        TestCatalogDbContext db, IEnumerable<Guid> ids, string key, string[] values, FilterAssignMode mode) =>
        new AssignFilterValuesCommandHandler(db).Handle(
            new AssignFilterValuesCommand(ids.ToList(), key, values, mode), CancellationToken.None);

    [Fact]
    public async Task CreatingAFilter_GivesItASlugKeyAndSlugValues()
    {
        var (db, _) = await SeedAsync(0);

        var filter = await CreateAsync(db, GameMode());

        Assert.Equal("game-mode", filter.Key);
        Assert.Equal(["single-player", "multiplayer"], filter.Values.Select(v => v.Value).ToArray());
    }

    [Fact]
    public async Task ANewFilterCannotReuseTheKeyOfAnOptionGroupOrAnotherFilter()
    {
        var (db, products) = await SeedAsync(1);
        db.ProductOptionGroups.Add(ProductOptionGroup.Create(products[0].Id, "game-mode", "Game mode"));
        await db.SaveChangesAsync();

        var result = await new SaveFilterDefinitionCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin"))
            .Handle(new SaveFilterDefinitionCommand(null, GameMode()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Filters.KeyTaken", result.Error.Code);
    }

    [Fact]
    public async Task AssigningValues_MergeKeepsOldOnes_ReplaceSwapsThem_RemoveTakesThemOff()
    {
        var (db, products) = await SeedAsync(2);
        await CreateAsync(db, GameMode());
        var ids = products.Select(p => p.Id).ToList();

        await AssignAsync(db, ids, "game-mode", ["single-player"], FilterAssignMode.Merge);
        var merged = await AssignAsync(db, ids, "game-mode", ["multiplayer"], FilterAssignMode.Merge);
        Assert.Equal(4, await db.ProductFilterValues.CountAsync());
        Assert.Equal(2, merged.Value.ProductsChanged);

        await AssignAsync(db, ids, "game-mode", ["multiplayer"], FilterAssignMode.Replace);
        Assert.Equal(2, await db.ProductFilterValues.CountAsync());

        await AssignAsync(db, ids, "game-mode", ["multiplayer"], FilterAssignMode.Remove);
        Assert.Equal(0, await db.ProductFilterValues.CountAsync());
    }

    [Fact]
    public async Task AssigningTheSameValuesTwice_DoesNotDuplicateOrReportChanges()
    {
        var (db, products) = await SeedAsync(1);
        await CreateAsync(db, GameMode());

        await AssignAsync(db, [products[0].Id], "game-mode", ["single-player"], FilterAssignMode.Merge);
        var again = await AssignAsync(db, [products[0].Id], "game-mode", ["single-player"], FilterAssignMode.Merge);

        Assert.Equal(1, await db.ProductFilterValues.CountAsync());
        Assert.Equal(0, again.Value.ProductsChanged);
    }

    [Fact]
    public async Task UnknownValuesAreReportedNotCreated_AndASingleChoiceFilterKeepsOneValue()
    {
        var (db, products) = await SeedAsync(1);
        await CreateAsync(db, GameMode(multi: false));

        var result = await AssignAsync(db, [products[0].Id], "game-mode", ["single-player", "multiplayer", "co-op"], FilterAssignMode.Merge);

        Assert.Equal(["co-op"], result.Value.UnknownValues.ToArray());
        Assert.Equal(1, await db.ProductFilterValues.CountAsync());
        Assert.Equal(2, await db.FilterValues.CountAsync());
    }

    [Fact]
    public async Task TheStorefrontOffersTheFilter_WithCountsOnlyForProductsInView()
    {
        var (db, products) = await SeedAsync(3);
        await CreateAsync(db, GameMode());
        await AssignAsync(db, [products[0].Id, products[1].Id], "game-mode", ["single-player"], FilterAssignMode.Merge);
        await AssignAsync(db, [products[1].Id], "game-mode", ["multiplayer"], FilterAssignMode.Merge);

        var result = await new GetProductFacetsQueryHandler(db).Handle(new GetProductFacetsQuery(null, null, null), CancellationToken.None);

        var group = Assert.Single(result.Value, g => g.Key == "game-mode");
        Assert.True(group.IsCustom);
        Assert.Equal(2, group.Options.Single(o => o.Value == "single-player").Count);
        Assert.Equal(1, group.Options.Single(o => o.Value == "multiplayer").Count);
        Assert.Equal("نمط اللعب", group.DisplayNameAr);
    }

    [Fact]
    public async Task ChoosingAValue_NarrowsTheProductListToThoseProducts()
    {
        var (db, products) = await SeedAsync(3);
        await CreateAsync(db, GameMode());
        await AssignAsync(db, [products[2].Id], "game-mode", ["multiplayer"], FilterAssignMode.Merge);

        var matching = ProductQueryFilters.ApplyAttributeFilters(
            db.Products,
            db,
            new Dictionary<string, IReadOnlyList<string>> { ["game-mode"] = ["multiplayer"] });

        Assert.Equal([products[2].Id], await matching.Select(p => p.Id).ToArrayAsync());
    }

    [Fact]
    public async Task ASwitchedOffFilterAndAValueWithNoProductsStayHidden_UnlessShowWhenEmptyIsOn()
    {
        var (db, products) = await SeedAsync(1);
        var filter = await CreateAsync(db, GameMode());
        await AssignAsync(db, [products[0].Id], "game-mode", ["single-player"], FilterAssignMode.Merge);

        var hidden = await new GetProductFacetsQueryHandler(db).Handle(new GetProductFacetsQuery(null, null, null), CancellationToken.None);
        Assert.Equal(["single-player"], hidden.Value.Single(g => g.Key == "game-mode").Options.Select(o => o.Value).ToArray());

        await new SaveFilterDefinitionCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin")).Handle(
            new SaveFilterDefinitionCommand(filter.Id, GameMode(showWhenEmpty: true)), CancellationToken.None);
        var shown = await new GetProductFacetsQueryHandler(db).Handle(new GetProductFacetsQuery(null, null, null), CancellationToken.None);
        Assert.Equal(2, shown.Value.Single(g => g.Key == "game-mode").Options.Count);

        await new SaveFilterDefinitionCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin")).Handle(
            new SaveFilterDefinitionCommand(filter.Id, GameMode() with { IsActive = false }), CancellationToken.None);
        var off = await new GetProductFacetsQueryHandler(db).Handle(new GetProductFacetsQuery(null, null, null), CancellationToken.None);
        Assert.DoesNotContain(off.Value, g => g.Key == "game-mode");
    }

    [Fact]
    public async Task AFilterLimitedToACategoryIsHiddenElsewhere()
    {
        var (db, products) = await SeedAsync(1);
        var other = Category.Create("ب", "PlayStation", $"ps-{Guid.NewGuid():N}");
        db.Categories.Add(other);
        await db.SaveChangesAsync();
        await CreateAsync(db, GameMode(scope: [products[0].CategoryId]));
        await AssignAsync(db, [products[0].Id], "game-mode", ["single-player"], FilterAssignMode.Merge);

        var inside = await new GetProductFacetsQueryHandler(db).Handle(new GetProductFacetsQuery(null, products[0].CategoryId, null), CancellationToken.None);
        var outside = await new GetProductFacetsQueryHandler(db).Handle(new GetProductFacetsQuery(null, other.Id, null), CancellationToken.None);

        Assert.Contains(inside.Value, g => g.Key == "game-mode");
        Assert.DoesNotContain(outside.Value, g => g.Key == "game-mode");
    }

    [Fact]
    public async Task RemovingAValueUntagsItsProducts_AndDeletingTheFilterLeavesTheProducts()
    {
        var (db, products) = await SeedAsync(1);
        var filter = await CreateAsync(db, GameMode());
        await AssignAsync(db, [products[0].Id], "game-mode", ["single-player", "multiplayer"], FilterAssignMode.Merge);

        var onlyMulti = GameMode() with { Values = [new FilterValueDto(null, "multiplayer", "Multiplayer", "جماعي", 0, true)] };
        await new SaveFilterDefinitionCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin"))
            .Handle(new SaveFilterDefinitionCommand(filter.Id, onlyMulti), CancellationToken.None);
        Assert.Equal(1, await db.ProductFilterValues.CountAsync());

        await new DeleteFilterDefinitionCommandHandler(db).Handle(new DeleteFilterDefinitionCommand(filter.Id), CancellationToken.None);
        Assert.Equal(0, await db.ProductFilterValues.CountAsync());
        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public async Task ThePriceRangeSpansTheRealSellingPricesInView()
    {
        var (db, _) = await SeedAsync(3);

        var result = await new GetProductPriceRangeQueryHandler(db).Handle(
            new GetProductPriceRangeQuery(null, null, null), CancellationToken.None);

        Assert.Equal(10m, result.Value.Min);
        Assert.Equal(30m, result.Value.Max);
    }
}
