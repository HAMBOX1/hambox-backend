using HAMBOX.Application.Fulfillment;
using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// The authoritative, server-side price and supplier selection for one checkout line — computed fresh
/// here, never trusted from the cart's own (possibly stale) <c>UnitPrice</c> or from anything the
/// client sends. <see cref="SelectedSupplierId"/>/etc. are null for lines not priced from a supplier
/// (manual-only variants, or a variant with no eligible supplier right now).
/// </summary>
internal sealed record ResolvedLinePricing(
    Guid ProductId,
    Guid? ProductVariantId,
    decimal UnitPrice,
    Guid? SelectedSupplierId,
    Guid? SelectedSupplierProductMappingId,
    decimal? SupplierBuyingPriceAtOrderTime,
    decimal? MarginPercentAppliedAtOrderTime);

internal sealed record CartLineValidationResult(IReadOnlyList<ResolvedLinePricing> Lines);

/// <summary>
/// The per-line purchasability checks every checkout path (synchronous providers, DOT) must run
/// before reserving inventory: product active/restricted/released, and every line resolves to a
/// real, inventory-backed variant that can actually be fulfilled — either enough manual stock, or a
/// READY automated-supplier route, whichever the variant's <c>FulfillmentMode</c> allows. Also resolves
/// the authoritative <see cref="ResolvedLinePricing"/> for every line — see that type's remarks for why
/// this must never come from the client or from a stale cart row. Extracted out of the original single
/// synchronous <c>CheckoutCommandHandler</c> so DOT's separate initiate handler doesn't duplicate it.
/// </summary>
internal sealed class CartLineValidator(
    IInventoryEngine inventoryEngine,
    IFulfillmentRouter fulfillmentRouter,
    ISupplierPricingEngine supplierPricingEngine)
{
    public async Task<Result<CartLineValidationResult>> ValidateAsync(
        ShoppingCart cart,
        IReadOnlyDictionary<Guid, Product> products,
        IReadOnlyDictionary<Guid, ProductVariant> variants,
        IReadOnlyDictionary<Guid, VariantStockSnapshot> variantStock,
        IReadOnlyDictionary<Guid, ProductAccessInfo> productAccess,
        MembershipAccessInfo access,
        CancellationToken cancellationToken)
    {
        var resolvedLines = new List<ResolvedLinePricing>();

        foreach (var item in cart.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return Result.Failure<CartLineValidationResult>(CommerceErrors.ProductNotFound);
            }

            if (product.Status != ProductStatus.Active)
            {
                return Result.Failure<CartLineValidationResult>(CatalogErrors.ProductNotActive);
            }

            if (productAccess.TryGetValue(item.ProductId, out var itemAccess) && itemAccess is { IsRestricted: true, HasAccess: false })
            {
                return Result.Failure<CartLineValidationResult>(CommerceErrors.ProductMembersOnly(itemAccess.RequiredPlanNames));
            }

            if (product.PublicReleaseOnUtc is DateTime releaseOnUtc && releaseOnUtc > DateTime.UtcNow)
            {
                var earlyAccessStartsUtc = releaseOnUtc.AddDays(-access.EarlyAccessDays);
                if (DateTime.UtcNow < earlyAccessStartsUtc)
                {
                    return Result.Failure<CartLineValidationResult>(CommerceErrors.ProductNotYetReleased(releaseOnUtc));
                }
            }

            if (item.ProductVariantId is Guid variantId)
            {
                if (!variants.TryGetValue(variantId, out var variant) || variant.Status != ProductVariantStatus.Active || !variant.IsVisible)
                {
                    return Result.Failure<CartLineValidationResult>(CatalogErrors.VariantNotFound);
                }

                if (variant.ProductId != item.ProductId)
                {
                    return Result.Failure<CartLineValidationResult>(CatalogErrors.VariantNotFound);
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
                    return Result.Failure<CartLineValidationResult>(CatalogErrors.InsufficientInventory);
                }

                // Authoritative price, resolved fresh here — never trusts the cart row's own (possibly
                // stale) UnitPrice, and definitely never anything the client could submit. Only
                // SupplierFirst/SupplierOnly variants ever get a supplier-derived price; everything else
                // is the unchanged PriceOverride ?? Product.Price chain via EffectivePriceResolver.
                if (variant.FulfillmentMode is FulfillmentMode.SupplierFirst or FulfillmentMode.SupplierOnly)
                {
                    var pricing = await supplierPricingEngine.ResolveAsync(
                        new SupplierRoutingRequest(item.ProductId, variantId, item.Quantity), cancellationToken);

                    var winner = pricing.RankedBySellingPriceAscending.Count > 0
                        ? pricing.RankedBySellingPriceAscending[0]
                        : null;

                    resolvedLines.Add(new ResolvedLinePricing(
                        item.ProductId,
                        variantId,
                        EffectivePriceResolver.Resolve(variant, product, winner?.SellingPrice),
                        winner?.SupplierId,
                        winner?.SupplierProductMappingId,
                        winner?.CostInBaseCurrency,
                        winner?.MarginPercentApplied));
                }
                else
                {
                    resolvedLines.Add(new ResolvedLinePricing(
                        item.ProductId, variantId, EffectivePriceResolver.Resolve(variant, product), null, null, null, null));
                }
            }
            else if (await inventoryEngine.ProductHasVariantsAsync(item.ProductId, cancellationToken))
            {
                return Result.Failure<CartLineValidationResult>(CatalogErrors.VariantRequired);
            }
            else
            {
                // No variant on this cart line and the product has no active, visible variant at
                // all — there is no inventory-backed way to ever deliver this product. Block the
                // order instead of falling back to the legacy Product.StockQuantity counter (CSV
                // import bookkeeping only, not a real digital code) and fabricating a license key.
                return Result.Failure<CartLineValidationResult>(CatalogErrors.ProductNotPurchasable);
            }
        }

        return Result.Success(new CartLineValidationResult(resolvedLines));
    }

    /// <summary>
    /// Refreshes every cart line's <c>UnitPrice</c> in place to the just-resolved authoritative price —
    /// called by every checkout entry point immediately after a successful <see cref="ValidateAsync"/>,
    /// before promotion evaluation/order-amount calculation reads <c>cart.Items</c>, so the promotion
    /// engine, the order totals, and the created <c>OrderItem</c> rows can never disagree about price
    /// (a stale cart-add-time price is exactly the gap this closes — see the type's own remarks).
    /// </summary>
    public static void ApplyResolvedPricing(ShoppingCart cart, CartLineValidationResult validation)
    {
        foreach (var resolved in validation.Lines)
        {
            var existing = cart.Items.First(i => i.ProductId == resolved.ProductId && i.ProductVariantId == resolved.ProductVariantId);
            cart.AddOrUpdateItem(resolved.ProductId, existing.Quantity, resolved.UnitPrice, resolved.ProductVariantId);
        }
    }
}
