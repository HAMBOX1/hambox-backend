using HAMBOX.Modules.Catalog.Domain.Products;

namespace HAMBOX.UnitTests.Catalog.Products;

public class ProductCategoryAssignmentTests
{
    private static Product CreateProduct(Guid? categoryId = null) =>
        Product.Create("اسم", "Name", "وصف", "Description", 9.99m, categoryId ?? Guid.NewGuid());

    [Fact]
    public void SetAdditionalCategories_MultipleIds_AssignsAllAsAdditional()
    {
        var product = CreateProduct();
        var additional = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        product.SetAdditionalCategories(additional);

        Assert.Equal(additional.Length, product.AdditionalCategories.Count);
        Assert.Equal(additional.ToHashSet(), product.AdditionalCategories.Select(pc => pc.CategoryId).ToHashSet());
        Assert.All(product.AdditionalCategories, pc => Assert.Equal(product.Id, pc.ProductId));
    }

    [Fact]
    public void SetAdditionalCategories_DuplicateIdsInInput_CollapsesToDistinctSet()
    {
        var product = CreateProduct();
        var categoryId = Guid.NewGuid();

        product.SetAdditionalCategories([categoryId, categoryId, categoryId]);

        Assert.Single(product.AdditionalCategories);
    }

    [Fact]
    public void SetAdditionalCategories_ContainsPrimaryCategory_Throws()
    {
        var primaryCategoryId = Guid.NewGuid();
        var product = CreateProduct(primaryCategoryId);

        Assert.Throws<ArgumentException>(() =>
            product.SetAdditionalCategories([primaryCategoryId, Guid.NewGuid()]));
    }

    [Fact]
    public void SetAdditionalCategories_CalledTwice_ReplacesRatherThanAppends()
    {
        var product = CreateProduct();
        var firstBatch = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var secondBatch = new[] { Guid.NewGuid() };

        product.SetAdditionalCategories(firstBatch);
        product.SetAdditionalCategories(secondBatch);

        Assert.Single(product.AdditionalCategories);
        Assert.Equal(secondBatch[0], product.AdditionalCategories.Single().CategoryId);
    }

    [Fact]
    public void ChangeCategory_NewPrimaryWasPreviouslyAdditional_DropsItFromAdditionalSet()
    {
        var product = CreateProduct();
        var futurePrimary = Guid.NewGuid();
        product.SetAdditionalCategories([futurePrimary, Guid.NewGuid()]);

        product.ChangeCategory(futurePrimary);

        Assert.Equal(futurePrimary, product.CategoryId);
        Assert.DoesNotContain(product.AdditionalCategories, pc => pc.CategoryId == futurePrimary);
        Assert.Single(product.AdditionalCategories);
    }

    [Fact]
    public void NewlyCreatedProduct_HasNoAdditionalCategories_PrimaryOnlyByDefault()
    {
        // Mirrors every product created before this feature existed: the primary category
        // (former single CategoryId) carries over untouched, with an empty additional set —
        // exactly what the additive migration preserves for existing rows.
        var categoryId = Guid.NewGuid();
        var product = CreateProduct(categoryId);

        Assert.Equal(categoryId, product.CategoryId);
        Assert.Empty(product.AdditionalCategories);
    }
}
