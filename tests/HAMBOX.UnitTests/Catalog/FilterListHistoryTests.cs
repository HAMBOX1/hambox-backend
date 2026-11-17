using HAMBOX.Modules.Catalog.Application.Features.Categories.CategoryFilters;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog;

/// <summary>Every change to a filter list is remembered (who, when, what it was) and can be brought back.</summary>
public sealed class FilterListHistoryTests
{
    private static CategoryFilterInput Item(string key, bool visible = true) => new(key, null, null, visible);

    private static async Task SaveAsync(TestCatalogDbContext db, Guid categoryId, params CategoryFilterInput[] items) =>
        await new SaveCategoryFilterConfigCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin"))
            .Handle(new SaveCategoryFilterConfigCommand(categoryId, items), CancellationToken.None);

    [Fact]
    public async Task EachSaveKeepsTheVersionItReplaced_WithWhoChangedIt()
    {
        var db = TestCatalogDbContextFactory.Create();
        var category = Guid.NewGuid();

        await SaveAsync(db, category, Item("region"), Item("platform"));
        Assert.Empty(await db.FilterListHistories.ToListAsync());

        await SaveAsync(db, category, Item("platform"));
        var history = await db.FilterListHistories.SingleAsync();
        Assert.Equal("Saved", history.Action);
        Assert.Equal(2, history.ItemCount);
        Assert.Equal("admin-1", history.ChangedBy);
    }

    [Fact]
    public async Task RestoringAnEarlierVersionBringsBackItsOrder_AndKeepsTheCurrentOneToo()
    {
        var db = TestCatalogDbContextFactory.Create();
        var category = Guid.NewGuid();
        await SaveAsync(db, category, Item("region"), Item("platform"));
        await SaveAsync(db, category, Item("platform"));
        var earlier = await db.FilterListHistories.SingleAsync();

        var restored = await new RestoreFilterListCommandHandler(db, new FakeCurrentUserService("admin-2", "Admin"))
            .Handle(new RestoreFilterListCommand(category, earlier.Id), CancellationToken.None);

        Assert.True(restored.IsSuccess);
        var keys = await db.CategoryFacetSettings.Where(s => s.CategoryId == category).OrderBy(s => s.SortOrder).Select(s => s.GroupKey).ToListAsync();
        Assert.Equal(["region", "platform"], keys);
        Assert.Equal(2, await db.FilterListHistories.CountAsync());
    }

    [Fact]
    public async Task AVersionOfAnotherCategoryCannotBeRestored()
    {
        var db = TestCatalogDbContextFactory.Create();
        var one = Guid.NewGuid();
        await SaveAsync(db, one, Item("region"));
        await SaveAsync(db, one, Item("platform"));
        var version = await db.FilterListHistories.SingleAsync();

        var result = await new RestoreFilterListCommandHandler(db, new FakeCurrentUserService("admin-1", "Admin"))
            .Handle(new RestoreFilterListCommand(Guid.NewGuid(), version.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
