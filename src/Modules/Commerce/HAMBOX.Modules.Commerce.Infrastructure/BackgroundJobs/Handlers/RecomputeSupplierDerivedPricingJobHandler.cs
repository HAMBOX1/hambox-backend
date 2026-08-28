using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Recurring recompute of the persisted <see cref="SupplierDerivedPrice"/> cache — the only thing
/// storefront/checkout pricing ever reads for a <c>SupplierFirst</c>/<c>SupplierOnly</c> variant (via
/// <c>IFulfillmentRouter.GetEffectivePriceOverridesBulkAsync</c>), so this is what keeps that cache from
/// going stale after a supplier availability sync, a mapping edit, or a margin change. Runs on the same
/// cadence as <c>SupplierAvailabilitySyncJobHandler</c> (5 minutes) — deliberately lives in Commerce
/// (not Suppliers) since <see cref="ISupplierPricingEngine"/> must never be reachable from Suppliers or
/// Catalog (it's the one place supplier acquisition cost is computed).
/// </summary>
/// <remarks>
/// A variant with zero eligible candidates gets its row deleted (falls back to
/// <c>PriceOverride ?? Product.Price</c>) — this is the intended, safe answer for "no automated
/// supplier can currently fulfill this," never a $0 price. A variant that still has an eligible
/// candidate simply gets its row overwritten with the fresh winner; existing orders are never touched,
/// since <c>OrderItem</c>'s snapshot columns are copied once at checkout and never re-read from here.
/// </remarks>
internal sealed class RecomputeSupplierDerivedPricingJobHandler(
    IBackgroundJobSerializer serializer,
    ICatalogDbContext catalogDb,
    ISuppliersDbContext suppliersDb,
    ISupplierPricingEngine pricingEngine,
    ILogger<RecomputeSupplierDerivedPricingJobHandler> logger) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.RecomputeSupplierDerivedPricing;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var variants = await catalogDb.ProductVariants.AsNoTracking()
            .Where(v => v.FulfillmentMode == FulfillmentMode.SupplierFirst || v.FulfillmentMode == FulfillmentMode.SupplierOnly)
            .Select(v => new { v.Id, v.ProductId })
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
            return;
        }

        var existingByVariant = await suppliersDb.SupplierDerivedPrices
            .Where(p => variants.Select(v => v.Id).Contains(p.InternalProductVariantId))
            .ToDictionaryAsync(p => p.InternalProductVariantId, cancellationToken);

        var recomputed = 0;
        var removed = 0;

        foreach (var variant in variants)
        {
            var result = await pricingEngine.ResolveAsync(
                new SupplierRoutingRequest(variant.ProductId, variant.Id, Quantity: 1), cancellationToken);

            var winner = result.RankedBySellingPriceAscending.Count > 0 ? result.RankedBySellingPriceAscending[0] : null;
            existingByVariant.TryGetValue(variant.Id, out var existingRow);

            if (winner is null)
            {
                if (existingRow is not null)
                {
                    suppliersDb.SupplierDerivedPrices.Remove(existingRow);
                    removed++;
                }

                continue;
            }

            if (existingRow is not null)
            {
                existingRow.Recompute(winner.SellingPrice, winner.SupplierId, winner.SupplierProductMappingId, winner.MarginPercentApplied, result.BaseCurrency);
            }
            else
            {
                suppliersDb.SupplierDerivedPrices.Add(SupplierDerivedPrice.Create(
                    variant.ProductId, variant.Id, winner.SellingPrice, winner.SupplierId, winner.SupplierProductMappingId,
                    winner.MarginPercentApplied, result.BaseCurrency));
            }

            recomputed++;
        }

        await suppliersDb.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Supplier derived pricing recompute: {Recomputed} variant(s) priced, {Removed} row(s) removed (no eligible supplier), {Total} variant(s) scanned.",
            recomputed, removed, variants.Count);
    }
}
