using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;

namespace HAMBOX.Modules.Catalog.Application.Services;

/// <summary>
/// The single, centralized "what does the customer pay for this variant" formula — replaces the
/// previously duplicated <c>variant.PriceOverride ?? product.Price</c> inline expression that existed
/// independently in cart add/update, wishlist-to-cart, price-drop alerts, alert subscriptions, and the
/// storefront PDP builder. <paramref name="supplierEffectivePrice"/> is the persisted, cost-free
/// supplier-derived price (from <c>IFulfillmentRouter.GetEffectivePriceOverridesBulkAsync</c>) — present
/// only for <c>SupplierFirst</c>/<c>SupplierOnly</c> variants that currently have an eligible supplier;
/// every other variant simply passes <see langword="null"/> and gets the unchanged legacy behavior.
/// </summary>
public static class EffectivePriceResolver
{
    public static decimal Resolve(ProductVariant variant, Product product, decimal? supplierEffectivePrice = null) =>
        supplierEffectivePrice ?? variant.PriceOverride ?? product.Price;

    public static decimal Resolve(decimal? variantPriceOverride, decimal productPrice, decimal? supplierEffectivePrice = null) =>
        supplierEffectivePrice ?? variantPriceOverride ?? productPrice;
}
