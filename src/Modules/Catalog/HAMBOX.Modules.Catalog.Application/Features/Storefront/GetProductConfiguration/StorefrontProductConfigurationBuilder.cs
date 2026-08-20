using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
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
        IReadOnlyDictionary<Guid, VariantStockSnapshot> stock,
        IReadOnlyDictionary<Guid, FulfillmentReadiness> readiness)
    {
        var optionGroupDtos = optionGroups.Select(g => new ProductOptionGroupDto(
            g.Id, g.ProductId, g.ParentOptionId, g.Key, g.DisplayName, g.SortOrder, g.IsRequired,
            g.Options.OrderBy(o => o.SortOrder)
                .Select(o => new ProductOptionDto(o.Id, o.OptionGroupId, o.Value, o.Label, o.SortOrder, o.DescriptionHtml))
                .ToList())).ToList();

        var validCombinationKeys = VariantCombinationHelper.BuildValidCombinationKeys(optionGroups);

        var variantDtos = variants.Select(v =>
        {
            stock.TryGetValue(v.Id, out var s);
            var manualAvailable = s?.Available ?? 0;

            // Quantity=1 is the correct granularity here — this DTO answers "can a customer add ONE
            // of this to their cart," matching CartLineValidator's own per-unit check at checkout
            // (neither this nor the checkout-time router is quantity-N aware for the supplier path;
            // see FulfillmentAvailability's doc comment).
            var supplierReady = readiness.TryGetValue(v.Id, out var r) && r.SupplierReady;
            var isPurchasable = FulfillmentAvailability.IsAvailable(v.FulfillmentMode, manualAvailable > 0, supplierReady);

            var optionIds = v.SelectedOptions.Select(o => o.OptionId).ToList();
            var isCompleteCombination = VariantCombinationHelper.IsCompleteCombination(optionIds, validCombinationKeys, optionGroups);

            return new StorefrontVariantDto(
                v.Id,
                v.Sku,
                v.PriceOverride ?? product.Price,
                v.ComparePrice,
                manualAvailable,
                s?.IsLowStock ?? false,
                !isPurchasable,
                optionIds,
                isCompleteCombination);
        }).ToList();

        return new StorefrontProductConfigurationDto(product.Id, product.Price, optionGroupDtos, variantDtos);
    }
}
