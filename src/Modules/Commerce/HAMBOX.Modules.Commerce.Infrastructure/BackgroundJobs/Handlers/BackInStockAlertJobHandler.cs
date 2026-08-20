using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Recurring scan for back-in-stock alerts. Never touches the catalog at large — only variants that
/// have at least one active <see cref="CustomerAlertType.BackInStock"/> subscription. Re-derives
/// "genuinely purchasable" fresh from <see cref="IInventoryEngine"/> plus the variant/product
/// Status/IsVisible gates every pass, rather than reacting to any single mutation, so it catches
/// every path that can make a variant available again (code import, reservation release/expiry,
/// returned-to-stock, re-enabled code, or a variant/product reactivation with no code change at all)
/// — see the architecture audit's availability-lifecycle finding.
/// </summary>
internal sealed class BackInStockAlertJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext commerceDb,
    ICatalogDbContext catalogDb,
    IInventoryEngine inventory,
    ICommunicationService communication) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.ScanBackInStockAlerts;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var variantIds = await commerceDb.CustomerAlertSubscriptions
            .Where(s => s.AlertType == CustomerAlertType.BackInStock && s.IsActive)
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

        var stock = await inventory.GetVariantStockBulkAsync(variantIds, cancellationToken);

        foreach (var variantId in variantIds)
        {
            if (!variants.TryGetValue(variantId, out var variant) || !products.TryGetValue(variant.ProductId, out var product))
            {
                continue; // Variant or product was permanently deleted since the subscription was created.
            }

            var isGenuinelyPurchasable =
                stock.TryGetValue(variantId, out var snapshot) && !snapshot.IsOutOfStock
                && variant.Status == ProductVariantStatus.Active && variant.IsVisible
                && product.Status == ProductStatus.Active;

            if (!isGenuinelyPurchasable)
            {
                continue;
            }

            var subscriptions = await commerceDb.CustomerAlertSubscriptions
                .Where(s => s.AlertType == CustomerAlertType.BackInStock
                    && s.VariantId == variantId
                    && s.IsActive
                    && s.NotifiedOnUtc == null
                    && s.UserId != null) // Unclaimed guest rows have no identity to notify — left untouched until claimed.
                .ToListAsync(cancellationToken);

            foreach (var subscription in subscriptions)
            {
                await communication.SendAsync(new CommunicationRequest(
                    UserId: subscription.UserId!,
                    TemplateKey: "BackInStockAlert",
                    Category: CommunicationCategory.BackInStock,
                    Variables: new Dictionary<string, string>
                    {
                        ["ProductName"] = product.NameEn,
                        ["VariantLabel"] = variant.Sku,
                    },
                    RelatedEntityType: "ProductVariant",
                    RelatedEntityId: variantId.ToString(),
                    ActionUrl: $"/products/{product.Id}"), cancellationToken);

                // Marked and saved immediately per subscriber (not batched at the end) — if this job
                // is retried after a partial failure, already-processed subscribers are already
                // IsActive=false/NotifiedOnUtc-set and the selection query above will never see them
                // again, so a retry can never send a second notification for the same subscription.
                subscription.MarkNotified();
                await commerceDb.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
