using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;

namespace HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration;

/// <summary>
/// Shared mapping logic behind the single-product and bulk storefront configuration queries —
/// keeps the option group/variant/stock projection identical between the two.
/// </summary>
internal static class StorefrontProductConfigurationBuilder
{
    public static StorefrontProductConfigurationDto Build(
        Product product,
        IReadOnlyList<ProductOptionGroup> optionGroups,
        IReadOnlyList<ProductVariant> variants,
        IReadOnlyDictionary<Guid, VariantStockSnapshot> stock)
    {
        var optionGroupDtos = optionGroups.Select(g => new ProductOptionGroupDto(
            g.Id, g.ProductId, g.ParentOptionId, g.Key, g.DisplayName, g.SortOrder, g.IsRequired,
            g.Options.OrderBy(o => o.SortOrder)
                .Select(o => new ProductOptionDto(o.Id, o.OptionGroupId, o.Value, o.Label, o.SortOrder, o.DescriptionHtml))
                .ToList())).ToList();

        var variantDtos = variants.Select(v =>
        {
            stock.TryGetValue(v.Id, out var s);
            return new StorefrontVariantDto(
                v.Id,
                v.Sku,
                v.PriceOverride ?? product.Price,
                v.ComparePrice,
                s?.Available ?? 0,
                s?.IsLowStock ?? false,
                s?.IsOutOfStock ?? true,
                v.SelectedOptions.Select(o => o.OptionId).ToList());
        }).ToList();

        return new StorefrontProductConfigurationDto(product.Id, product.Price, optionGroupDtos, variantDtos);
    }
}
