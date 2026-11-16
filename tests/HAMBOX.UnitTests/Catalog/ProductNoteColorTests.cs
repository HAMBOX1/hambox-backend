using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Products;

namespace HAMBOX.UnitTests.Catalog;

/// <summary>The colour of the note under a product's price, and a category's small card icon.</summary>
public sealed class ProductNoteColorTests
{
    private static Product NewProduct() => Product.Create("منتج", "Product", "d", "d", 10m, Guid.NewGuid());

    [Theory]
    [InlineData("#FF8800", "#ff8800")]
    [InlineData(" #12ab9F ", "#12ab9f")]
    public void KeepsAValidHexColour(string input, string expected)
    {
        var product = NewProduct();

        product.SetBadge("Get 2 points extra", null, input);

        Assert.Equal(expected, product.BadgeColor);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#fff")]
    [InlineData("#GGGGGG")]
    [InlineData("#ff8800; background:url(x)")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingThatIsNotAHexColourMeansTheDefault(string? input)
    {
        var product = NewProduct();

        product.SetBadge("Get 2 points extra", null, input);

        Assert.Null(product.BadgeColor);
    }

    [Fact]
    public void ClearingTheNoteAlsoClearsItsColour()
    {
        var product = NewProduct();
        product.SetBadge("Elite price $9.50", null, "#ff8800");

        product.SetBadge(null, null, "#ff8800");

        Assert.Null(product.BadgeTextEn);
        Assert.Null(product.BadgeColor);
    }

    [Fact]
    public void ACategoryIconCanBeSetAndCleared()
    {
        var category = Category.Create("ستيم", "Steam", "steam");

        category.SetIcon("  /uploads/steam.svg ");
        Assert.Equal("/uploads/steam.svg", category.IconUrl);

        category.SetIcon("   ");
        Assert.Null(category.IconUrl);
    }
}
