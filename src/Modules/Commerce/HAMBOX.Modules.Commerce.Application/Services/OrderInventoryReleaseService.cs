using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Releases inventory (digital codes and no-variant product stock) that a checkout committed,
/// mirroring the reverse of what <see cref="OrderFulfillmentService"/> and checkout commit.
/// </summary>
public sealed class OrderInventoryReleaseService
{
    private readonly ICatalogDbContext _catalogDb;
    private readonly IInventoryEngine _inventoryEngine;

    public OrderInventoryReleaseService(ICatalogDbContext catalogDb, IInventoryEngine inventoryEngine)
    {
        _catalogDb = catalogDb;
        _inventoryEngine = inventoryEngine;
    }

    /// <summary>
    /// Returns every sold digital code and committed no-variant product unit for <paramref name="order"/>
    /// back to sellable stock. Safe to call more than once for the same order — already-released codes and
    /// products are simply skipped.
    /// </summary>
    public async Task ReleaseAsync(Order order, string? performedByUserId, CancellationToken cancellationToken)
    {
        await _inventoryEngine.ReleaseSoldCodesForOrderAsync(order.Id, performedByUserId, cancellationToken);

        var noVariantItems = order.Items
            .Where(i => i.LineItemType == OrderLineItemType.Product
                && i.ProductVariantId is null
                && i.ProductId is Guid)
            .ToList();

        if (noVariantItems.Count > 0)
        {
            var productIds = noVariantItems.Select(i => i.ProductId!.Value).Distinct().ToList();
            var products = await _catalogDb.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var item in noVariantItems)
            {
                if (products.TryGetValue(item.ProductId!.Value, out var product))
                {
                    product.RestoreStock(item.Quantity);
                }
            }
        }

        await _catalogDb.SaveChangesAsync(cancellationToken);
    }
}
