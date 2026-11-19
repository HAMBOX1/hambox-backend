using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory;

/// <summary>A variant is sellable / stockable only when it picks one option from every option group that applies to it.</summary>
public sealed class VariantCompletenessTests
{
    // Region (Turkey, Ukraine) x Type (Essential, Extra) x Duration (1 month, 3 months) = 8 complete combinations.
    private sealed record Setup(Guid ProductId, ProductOptionGroup Region, ProductOptionGroup Type, ProductOptionGroup Duration);

    private static async Task<(Setup Setup, HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle.TestCatalogDbContext Db)> SeedAsync()
    {
        var db = TestCatalogDbContextFactory.Create();
        var product = Product.Create("منتج", "PlayStation Plus", "d", "d", 0m, Guid.NewGuid());
        db.Products.Add(product);

        var region = ProductOptionGroup.Create(product.Id, "region", "Region", 0);
        region.AddOption("turkey", "Turkey", 0);
        region.AddOption("ukraine", "Ukraine", 1);
        var type = ProductOptionGroup.Create(product.Id, "type", "Type", 1);
        type.AddOption("essential", "Essential", 0);
        type.AddOption("extra", "Extra", 1);
        var duration = ProductOptionGroup.Create(product.Id, "duration", "Duration", 2);
        duration.AddOption("1", "1 Month", 0);
        duration.AddOption("3", "3 Months", 1);
        db.ProductOptionGroups.AddRange(region, type, duration);
        await db.SaveChangesAsync();
        return (new Setup(product.Id, region, type, duration), db);
    }

    [Fact]
    public async Task OnlyAFullCombinationCountsAsComplete()
    {
        var (setup, db) = await SeedAsync();
        var turkey = setup.Region.Options.First(o => o.Value == "turkey").Id;
        var essential = setup.Type.Options.First(o => o.Value == "essential").Id;
        var oneMonth = setup.Duration.Options.First(o => o.Value == "1").Id;

        Assert.True(await VariantCompleteness.IsCompleteAsync(db, setup.ProductId, [turkey, essential, oneMonth], CancellationToken.None));
        Assert.False(await VariantCompleteness.IsCompleteAsync(db, setup.ProductId, [turkey, essential], CancellationToken.None));
        Assert.False(await VariantCompleteness.IsCompleteAsync(db, setup.ProductId, [turkey], CancellationToken.None));
        Assert.False(await VariantCompleteness.IsCompleteAsync(db, setup.ProductId, [], CancellationToken.None));
    }

    [Fact]
    public async Task ListsEveryCombination_AndMarksTheOnesThatAlreadyHaveAVariant()
    {
        var (setup, db) = await SeedAsync();
        var existing = ProductVariant.Create(setup.ProductId, "SKU-1");
        existing.SetOptions([
            setup.Region.Options.First(o => o.Value == "turkey").Id,
            setup.Type.Options.First(o => o.Value == "essential").Id,
            setup.Duration.Options.First(o => o.Value == "1").Id]);
        db.ProductVariants.Add(existing);
        await db.SaveChangesAsync();

        var result = await new GetVariantCombinationsQueryHandler(db).Handle(new GetVariantCombinationsQuery(setup.ProductId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value.Items.Count);
        Assert.Equal(1, result.Value.ExistingCount);
        Assert.Equal(7, result.Value.MissingCount);
        Assert.Contains(result.Value.Items, i => i.Labels.SequenceEqual(["Region: Turkey", "Type: Essential", "Duration: 1 Month"]) && i.Exists);
    }

    [Fact]
    public async Task GeneratingWithASelection_CreatesOnlyThoseCombinations()
    {
        var (setup, db) = await SeedAsync();
        var ukraine = setup.Region.Options.First(o => o.Value == "ukraine").Id;
        var essential = setup.Type.Options.First(o => o.Value == "essential").Id;
        var oneMonth = setup.Duration.Options.First(o => o.Value == "1").Id;
        var threeMonths = setup.Duration.Options.First(o => o.Value == "3").Id;
        var handler = new GenerateProductVariantsCommandHandler(db, new FakeCurrentUserService(null));

        var result = await handler.Handle(new GenerateProductVariantsCommand(
            setup.ProductId,
            [new[] { ukraine, essential, oneMonth }, new[] { ukraine, essential, threeMonths }, new[] { ukraine, essential }]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.CreatedCount);
        var variants = await db.ProductVariants.Include(v => v.SelectedOptions).ToListAsync();
        Assert.Equal(2, variants.Count);
        Assert.All(variants, v => Assert.Equal(3, v.SelectedOptions.Count));
    }

    [Fact]
    public async Task GeneratingWithoutASelection_StillCreatesEveryMissingCombination()
    {
        var (setup, db) = await SeedAsync();

        var result = await new GenerateProductVariantsCommandHandler(db, new FakeCurrentUserService(null))
            .Handle(new GenerateProductVariantsCommand(setup.ProductId), CancellationToken.None);

        Assert.Equal(8, result.Value.CreatedCount);
    }
}
