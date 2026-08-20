using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Reproduces the reported bug: a product's "Value" option group was added after its "Global"/"US"
/// variants already existed, so those variants carry real stock but omit a selection for "Value" —
/// the storefront can never resolve them through the picker, yet nothing stopped them from being
/// created/edited/duplicated that way. Create/Update/Duplicate must all reject an option-id set that
/// omits a reachable required-by-structure group (see <see cref="VariantCombinationHelper.Expand"/>).
/// </summary>
public sealed class VariantCombinationValidationTests
{
    private static async Task<Guid> SeedProductAsync(TestCatalogDbContext db)
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 10m, category.Id);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync(CancellationToken.None);
        return product.Id;
    }

    private static (ProductOptionGroup Region, ProductOptionGroup Value) SeedRegionValueGroups(TestCatalogDbContext db, Guid productId)
    {
        var region = ProductOptionGroup.Create(productId, "region", "Region", sortOrder: 0);
        region.AddOption("global", "Global", 0);
        region.AddOption("us", "US", 1);

        var value = ProductOptionGroup.Create(productId, "value", "Value", sortOrder: 1);
        value.AddOption("100", "100", 0);

        db.ProductOptionGroups.AddRange(region, value);
        return (region, value);
    }

    [Fact]
    public async Task Create_MissingValueGroup_FailsWithIncompleteVariantCombination_NothingInserted()
    {
        var db = TestCatalogDbContextFactory.Create();
        var productId = await SeedProductAsync(db);
        var (region, _) = SeedRegionValueGroups(db, productId);
        await db.SaveChangesAsync(CancellationToken.None);

        var globalOption = region.Options.Single(o => o.Value == "global");
        var handler = new CreateProductVariantCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            new CreateProductVariantCommand(productId, "SKU-GLOBAL", null, null, null, 0, null, 0, [globalOption.Id]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.IncompleteVariantCombination(["Value"]).Code, result.Error.Code);
        Assert.Contains("Value", result.Error.Description);
        Assert.False(await db.ProductVariants.AnyAsync());
    }

    [Fact]
    public async Task Create_CompleteCombination_Succeeds()
    {
        var db = TestCatalogDbContextFactory.Create();
        var productId = await SeedProductAsync(db);
        var (region, value) = SeedRegionValueGroups(db, productId);
        await db.SaveChangesAsync(CancellationToken.None);

        var usOption = region.Options.Single(o => o.Value == "us");
        var valueOption = value.Options.Single(o => o.Value == "100");
        var handler = new CreateProductVariantCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            new CreateProductVariantCommand(productId, "SKU-US-100", null, null, null, 0, null, 0, [usOption.Id, valueOption.Id]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.True(await db.ProductVariants.AnyAsync(v => v.Id == result.Value));
    }

    [Fact]
    public async Task Update_ChangingToMissingValueGroup_FailsWithIncompleteVariantCombination_NothingChanged()
    {
        var db = TestCatalogDbContextFactory.Create();
        var productId = await SeedProductAsync(db);
        var (region, value) = SeedRegionValueGroups(db, productId);
        await db.SaveChangesAsync(CancellationToken.None);
        var usOption = region.Options.Single(o => o.Value == "us");
        var valueOption = value.Options.Single(o => o.Value == "100");

        var variant = ProductVariant.Create(productId, "SKU-US-100");
        variant.SetOptions([usOption.Id, valueOption.Id]);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateProductVariantCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            new UpdateProductVariantCommand(
                variant.Id, "SKU-US-100", null, null, null, 0, ProductVariantStatus.Draft, true, null, 0, [usOption.Id]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.IncompleteVariantCombination(["Value"]).Code, result.Error.Code);
        var reloaded = await db.ProductVariants.Include(v => v.SelectedOptions).SingleAsync(v => v.Id == variant.Id);
        Assert.Equal(2, reloaded.SelectedOptions.Count);
    }

    [Fact]
    public async Task Duplicate_SourceAlreadyIncomplete_FailsWithIncompleteVariantCombination()
    {
        // Legacy broken data (e.g. created before the "Value" group existed) must not be allowed to
        // propagate further via Duplicate — the admin has to fix the source combination first.
        var db = TestCatalogDbContextFactory.Create();
        var productId = await SeedProductAsync(db);
        var (region, _) = SeedRegionValueGroups(db, productId);
        await db.SaveChangesAsync(CancellationToken.None);
        var globalOption = region.Options.Single(o => o.Value == "global");

        var variant = ProductVariant.Create(productId, "SKU-GLOBAL");
        variant.SetOptions([globalOption.Id]);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DuplicateProductVariantCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(new DuplicateProductVariantCommand(variant.Id, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.IncompleteVariantCombination(["Value"]).Code, result.Error.Code);
        Assert.Equal(1, await db.ProductVariants.CountAsync());
    }
}
