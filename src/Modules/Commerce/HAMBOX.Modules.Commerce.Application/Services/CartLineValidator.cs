using HAMBOX.Application.Fulfillment;
using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// The per-line purchasability checks every checkout path (synchronous providers, DOT) must run
/// before reserving inventory: product active/restricted/released, and every line resolves to a
/// real, inventory-backed variant that can actually be fulfilled — either enough manual stock, or a
/// READY automated-supplier route, whichever the variant's <c>FulfillmentMode</c> allows. Extracted
/// out of the original single synchronous <c>CheckoutCommandHandler</c> so DOT's separate initiate
/// handler doesn't duplicate it.
/// </summary>
internal sealed class CartLineValidator(IInventoryEngine inventoryEngine, IFulfillmentRouter fulfillmentRouter)
{
    public async Task<Result> ValidateAsync(
        ShoppingCart cart,
        IReadOnlyDictionary<Guid, Product> products,
        IReadOnlyDictionary<Guid, ProductVariant> variants,
        IReadOnlyDictionary<Guid, VariantStockSnapshot> variantStock,
        IReadOnlyDictionary<Guid, ProductAccessInfo> productAccess,
        MembershipAccessInfo access,
        CancellationToken cancellationToken)
    {
        foreach (var item in cart.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return Result.Failure(CommerceErrors.ProductNotFound);
            }

            if (product.Status != ProductStatus.Active)
            {
                return Result.Failure(CatalogErrors.ProductNotActive);
            }

            if (productAccess.TryGetValue(item.ProductId, out var itemAccess) && itemAccess is { IsRestricted: true, HasAccess: false })
            {
                return Result.Failure(CommerceErrors.ProductMembersOnly(itemAccess.RequiredPlanNames));
            }

            if (product.PublicReleaseOnUtc is DateTime releaseOnUtc && releaseOnUtc > DateTime.UtcNow)
            {
                var earlyAccessStartsUtc = releaseOnUtc.AddDays(-access.EarlyAccessDays);
                if (DateTime.UtcNow < earlyAccessStartsUtc)
                {
                    return Result.Failure(CommerceErrors.ProductNotYetReleased(releaseOnUtc));
                }
            }

            if (item.ProductVariantId is Guid variantId)
            {
                if (!variants.TryGetValue(variantId, out var variant) || variant.Status != ProductVariantStatus.Active || !variant.IsVisible)
                {
                    return Result.Failure(CatalogErrors.VariantNotFound);
                }

                if (variant.ProductId != item.ProductId)
                {
                    return Result.Failure(CatalogErrors.VariantNotFound);
                }

                var manualAvailable = variantStock.TryGetValue(variantId, out var stock) ? stock.Available : 0;
                var manualSufficient = manualAvailable >= item.Quantity;
                var readiness = await fulfillmentRouter.GetReadinessAsync(variantId, cancellationToken);

                // SupplierFirst/SupplierOnly deliberately never let manual stock happening to exist
                // substitute for a READY supplier route — SupplierFirst's manual fallback only ever
                // activates later, after a genuine, definite supplier failure (see
                // CommerceOrderLicenseKeyDeliverySink), never as a checkout-time shortcut that would
                // silently skip the supplier attempt this mode exists to make. This is the same
                // FulfillmentAvailability.IsAvailable rule the storefront display uses, so checkout can
                // never contradict what a customer was shown as purchasable.
                var canFulfill = FulfillmentAvailability.IsAvailable(readiness.Mode, manualSufficient, readiness.SupplierReady);

                if (!canFulfill)
                {
                    return Result.Failure(CatalogErrors.InsufficientInventory);
                }
            }
            else if (await inventoryEngine.ProductHasVariantsAsync(item.ProductId, cancellationToken))
            {
                return Result.Failure(CatalogErrors.VariantRequired);
            }
            else
            {
                // No variant on this cart line and the product has no active, visible variant at
                // all — there is no inventory-backed way to ever deliver this product. Block the
                // order instead of falling back to the legacy Product.StockQuantity counter (CSV
                // import bookkeeping only, not a real digital code) and fabricating a license key.
                return Result.Failure(CatalogErrors.ProductNotPurchasable);
            }
        }

        return Result.Success();
    }
}
