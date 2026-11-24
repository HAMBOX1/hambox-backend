using HAMBOX.Modules.Commerce.Domain.Business;

namespace HAMBOX.Modules.Commerce.Application.Features.Business;

/// <summary>Picks the wholesale price that applies to an account, product, variant and quantity.</summary>
public static class BusinessPricing
{
    /// <summary>
    /// The best matching price, or null when none applies. A price set for this account beats one for all businesses; a
    /// variant-specific price beats a product-wide one; a higher quantity tier beats a lower one; the cheapest wins a tie.
    /// </summary>
    public static BusinessPrice? Resolve(
        IEnumerable<BusinessPrice> prices, Guid accountId, Guid productId, Guid variantId, int quantity, DateTimeOffset now) =>
        prices
            .Where(p => p.AppliesAt(now)
                && p.ProductId == productId
                && (p.BusinessAccountId is null || p.BusinessAccountId == accountId)
                && (p.ProductVariantId is null || p.ProductVariantId == variantId)
                && p.MinQuantity <= quantity)
            .OrderByDescending(p => p.BusinessAccountId == accountId)
            .ThenByDescending(p => p.ProductVariantId == variantId)
            .ThenByDescending(p => p.MinQuantity)
            .ThenBy(p => p.UnitPriceUsd)
            .FirstOrDefault();
}
