using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Recurring scan for price-drop alerts. Only ever compares
/// <c>Variant.PriceOverride ?? Product.Price</c> — deliberately never promotions, coupons,
/// membership discounts, or currency conversion (see the architecture audit's pricing-lifecycle
/// scoping decision). Only variants with at least one active
/// <see cref="CustomerAlertType.PriceDrop"/> subscription are ever read.
/// </summary>
internal sealed class PriceDropAlertJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext commerceDb,
    ICatalogDbContext catalogDb,
    ICommunicationService communication) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.ScanPriceDropAlerts;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var variantIds = await commerceDb.CustomerAlertSubscriptions
            .Where(s => s.AlertType == CustomerAlertType.PriceDrop && s.IsActive)
            .Select(s => s.VariantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (variantIds.Count == 0)
        {
            return;
        }

        var variants = await catalogDb.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var products = await catalogDb.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var variantId in variantIds)
        {
            if (!variants.TryGetValue(variantId, out var variant) || !products.TryGetValue(variant.ProductId, out var product))
            {
                continue; // Variant or product was permanently deleted since the subscription was created.
            }

            var currentPrice = variant.PriceOverride ?? product.Price;

            var subscriptions = await commerceDb.CustomerAlertSubscriptions
                .Where(s => s.AlertType == CustomerAlertType.PriceDrop
                    && s.VariantId == variantId
                    && s.IsActive
                    && s.NotifiedOnUtc == null
                    && s.UserId != null) // Unclaimed guest rows have no identity to notify — left untouched until claimed.
                .ToListAsync(cancellationToken);

            foreach (var subscription in subscriptions)
            {
                // Genuine decrease only — unchanged or increased prices are a no-op, and the stored
                // baseline is never touched on a no-op, so a later drop below the ORIGINAL
                // subscribe-time price still fires even if the price moved up in between.
                if (subscription.LastObservedPrice is null || currentPrice >= subscription.LastObservedPrice)
                {
                    continue;
                }

                await communication.SendAsync(new CommunicationRequest(
                    UserId: subscription.UserId!,
                    TemplateKey: "PriceDropAlert",
                    Category: CommunicationCategory.PriceDrop,
                    Variables: new Dictionary<string, string>
                    {
                        ["ProductName"] = product.NameEn,
                        ["VariantLabel"] = variant.Sku,
                        ["OldPrice"] = subscription.LastObservedPrice.Value.ToString("0.00"),
                        ["NewPrice"] = currentPrice.ToString("0.00"),
                    },
                    RelatedEntityType: "ProductVariant",
                    RelatedEntityId: variantId.ToString(),
                    ActionUrl: $"/products/{product.Id}"), cancellationToken);

                // Marked and saved immediately per subscriber — same retry-safety reasoning as
                // BackInStockAlertJobHandler.
                subscription.MarkNotified();
                await commerceDb.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
