using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Holds manual stock for a pending hosted-gateway order while the customer pays (Scope §10.2-10.5), so a paid
/// order cannot find the stock already sold to someone else. Best-effort and never throws: a hold that cannot be
/// taken (no manual stock, supplier-only variant, transient error) simply leaves the existing "reserve at payment
/// confirmation" behaviour in place. Holds expire on their own (<c>ExpireInventoryReservations</c> sweep) and are
/// released immediately when the payment fails, expires or the order is cancelled.
/// </summary>
public sealed class OrderStockHoldService(
    IInventoryEngine inventoryEngine,
    ICatalogDbContext catalogDb,
    IFulfillmentRouter router,
    ILogger<OrderStockHoldService> logger)
{
    /// <summary>Extra time kept after the payment window so a callback that lands just after expiry still finds its stock.</summary>
    public static readonly TimeSpan LateCallbackGrace = TimeSpan.FromMinutes(10);

    /// <returns>The number of units held across all lines.</returns>
    public async Task<int> HoldAsync(Order order, DateTimeOffset paymentWindowEndsUtc, CancellationToken cancellationToken)
    {
        var held = 0;
        var holdUntil = paymentWindowEndsUtc + LateCallbackGrace;

        try
        {
            foreach (var item in order.Items.Where(i =>
                         i.LineItemType == OrderLineItemType.Product && i.ProductVariantId is not null))
            {
                var variantId = item.ProductVariantId!.Value;
                var readiness = await router.GetReadinessAsync(variantId, cancellationToken);
                if (!readiness.ManualAllowed)
                {
                    // SupplierOnly / SupplierFirst / ChatDelivery / Disabled never touch manual codes.
                    continue;
                }

                var reserved = await inventoryEngine.ReserveForOrderAsync(
                    variantId, item.Quantity, order.UserId, order.Id, holdUntil, cancellationToken);
                held += reserved.Count;
            }

            await catalogDb.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not hold stock for pending order {OrderId}; falling back to reserve-on-payment.", order.Id);
        }

        if (held > 0)
        {
            logger.LogInformation("Held {Units} unit(s) of stock for pending order {OrderId} until {HoldUntil:O}.", held, order.Id, holdUntil);
        }

        return held;
    }

    public async Task ReleaseAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var released = await inventoryEngine.ReleaseReservationsForOrderAsync(orderId, reason, cancellationToken);
            if (released > 0)
            {
                logger.LogInformation("Released {Units} held unit(s) of order {OrderId}: {Reason}.", released, orderId, reason);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The TTL sweep releases it anyway; never let a release hiccup change a payment outcome.
            logger.LogWarning(ex, "Could not release held stock for order {OrderId}; the expiry sweep will.", orderId);
        }
    }
}
