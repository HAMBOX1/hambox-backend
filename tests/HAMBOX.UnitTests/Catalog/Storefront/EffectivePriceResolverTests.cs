using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;

namespace HAMBOX.UnitTests.Catalog.Storefront;

/// <summary>
/// Covers <see cref="EffectivePriceResolver"/> — the single formula replacing the previously duplicated
/// <c>variant.PriceOverride ?? product.Price</c> inline expression across cart/wishlist/alerts/storefront
/// code. The supplier-override parameter is the one new behavior; everything else is a regression check
/// that the legacy fallback chain is unchanged.
/// </summary>
public sealed class EffectivePriceResolverTests
{
    private static (Product Product, ProductVariant Variant) CreateProductAndVariant(decimal productPrice, decimal? variantPriceOverride)
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", productPrice, category.Id);
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}", priceOverride: variantPriceOverride);
        return (product, variant);
    }

    [Fact]
    public void Resolve_NoSupplierOverride_NoVariantOverride_FallsBackToProductPrice()
    {
        var (product, variant) = CreateProductAndVariant(19.99m, variantPriceOverride: null);

        var price = EffectivePriceResolver.Resolve(variant, product, supplierEffectivePrice: null);

        Assert.Equal(19.99m, price);
    }

    [Fact]
    public void Resolve_NoSupplierOverride_VariantOverridePresent_UsesVariantOverride()
    {
        var (product, variant) = CreateProductAndVariant(19.99m, variantPriceOverride: 14.99m);

        var price = EffectivePriceResolver.Resolve(variant, product, supplierEffectivePrice: null);

        Assert.Equal(14.99m, price);
    }

    [Fact]
    public void Resolve_SupplierOverridePresent_TakesPrecedenceOverVariantOverrideAndProductPrice()
    {
        var (product, variant) = CreateProductAndVariant(19.99m, variantPriceOverride: 14.99m);

        var price = EffectivePriceResolver.Resolve(variant, product, supplierEffectivePrice: 8.28m);

        Assert.Equal(8.28m, price);
    }

    [Fact]
    public void Resolve_SupplierOverridePresent_NoVariantOverride_StillTakesPrecedenceOverProductPrice()
    {
        var (product, variant) = CreateProductAndVariant(19.99m, variantPriceOverride: null);

        var price = EffectivePriceResolver.Resolve(variant, product, supplierEffectivePrice: 8.28m);

        Assert.Equal(8.28m, price);
    }
}
