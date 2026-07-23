using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Domain.Products;

namespace HAMBOX.UnitTests.Catalog.Products;

/// <summary>
/// Covers the shared category filter (<see cref="ProductQueryFilters.ApplyBaseFilters"/>) that both
/// the storefront product list and the admin catalog list/facets/bulk-selection queries rely on:
/// a product must surface when browsing by its primary category OR any additional category.
/// </summary>
public class ProductQueryFiltersCategoryTests
{
    private static Product CreateProduct(Guid primaryCategoryId, params Guid[] additionalCategoryIds)
    {
        var product = Product.Create("اسم", "Name", "وصف", "Description", 9.99m, primaryCategoryId);
        product.SetAdditionalCategories(additionalCategoryIds);
        return product;
    }

    [Fact]
    public void ApplyBaseFilters_ByCategoryId_MatchesPrimaryCategory()
    {
        var categoryX = Guid.NewGuid();
        var categoryY = Guid.NewGuid();
        var matching = CreateProduct(categoryX);
        var nonMatching = CreateProduct(categoryY);

        var result = ProductQueryFilters.ApplyBaseFilters(
            new[] { matching, nonMatching }.AsQueryable(), null, categoryX, null).ToList();

        Assert.Single(result);
        Assert.Equal(matching.Id, result[0].Id);
    }

    [Fact]
    public void ApplyBaseFilters_ByCategoryId_MatchesAdditionalCategory()
    {
        var categoryX = Guid.NewGuid();
        var categoryY = Guid.NewGuid();
        var crossListed = CreateProduct(categoryY, categoryX);
        var nonMatching = CreateProduct(categoryY);

        var result = ProductQueryFilters.ApplyBaseFilters(
            new[] { crossListed, nonMatching }.AsQueryable(), null, categoryX, null).ToList();

        Assert.Single(result);
        Assert.Equal(crossListed.Id, result[0].Id);
    }

    [Fact]
    public void ApplyBaseFilters_ByCategoryId_MatchesEitherPrimaryOrAdditionalWithoutDuplicates()
    {
        var categoryX = Guid.NewGuid();
        var categoryY = Guid.NewGuid();
        var categoryZ = Guid.NewGuid();

        var primaryMatch = CreateProduct(categoryX);
        var additionalMatch = CreateProduct(categoryY, categoryX);
        var noMatch = CreateProduct(categoryZ);

        var result = ProductQueryFilters.ApplyBaseFilters(
            new[] { primaryMatch, additionalMatch, noMatch }.AsQueryable(), null, categoryX, null).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(
            new[] { primaryMatch.Id, additionalMatch.Id }.ToHashSet(),
            result.Select(p => p.Id).ToHashSet());
        Assert.DoesNotContain(result, p => p.Id == noMatch.Id);
    }

    [Fact]
    public void ApplyBaseFilters_ByCategoryId_NoMatches_ReturnsEmpty()
    {
        var product = CreateProduct(Guid.NewGuid());

        var result = ProductQueryFilters.ApplyBaseFilters(
            new[] { product }.AsQueryable(), null, Guid.NewGuid(), null).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyBaseFilters_NoCategoryId_ReturnsAllProducts()
    {
        var products = new[] { CreateProduct(Guid.NewGuid()), CreateProduct(Guid.NewGuid()) };

        var result = ProductQueryFilters.ApplyBaseFilters(products.AsQueryable(), null, null, null).ToList();

        Assert.Equal(2, result.Count);
    }
}
