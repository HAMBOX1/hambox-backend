using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Commerce's implementation of the generic <see cref="ISupplierFulfillmentDeliverySink"/> seam —
/// Suppliers invokes this whenever a supplier fulfillment reaches ANY terminal state (not only when
/// codes are present — see <see cref="ISupplierFulfillmentDeliverySink"/>'s remarks), synchronously or
/// via the background sweep's reconciliation, and this is where delivered codes actually become an
/// <see cref="OrderLicenseKey"/> the customer can see, using the exact same encryption-at-rest
/// mechanism (<c>CommerceDbContext.ApplyCodeEncryption</c>) manual-stock codes already go through.
/// It is also the one place <see cref="FulfillmentMode.SupplierFirst"/>'s manual fallback lives — see
/// <see cref="TryManualFallbackAsync"/>.
/// </summary>
internal sealed class CommerceOrderLicenseKeyDeliverySink(
    ICommerceDbContext commerceDb,
    ICatalogDbContext catalogDb,
    IInventoryEngine inventoryEngine,
    ICommerceTransactionService transactionService,
    ILogger<CommerceOrderLicenseKeyDeliverySink> logger)
    : ISupplierFulfillmentDeliverySink
{
    public async Task OnDeliveredAsync(
        Guid orderId,
        Guid orderItemId,
        Guid supplierId,
        Guid supplierProductMappingId,
        bool isScopeExhausted,
        IReadOnlyCollection<string> deliveredCodes,
        CancellationToken cancellationToken)
    {
        var order = await commerceDb.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        var item = order?.Items.FirstOrDefault(i => i.Id == orderItemId);
        if (order is null || item is null || item.ProductId is not Guid productId)
        {
            logger.LogError(
                "Supplier delivery arrived for an order/item that no longer resolves (order {OrderId}, item {OrderItemId}) — codes were not attached and require manual recovery.",
                orderId, orderItemId);
            return;
        }

        var existingHashes = await commerceDb.OrderLicenseKeys
            .Where(k => k.OrderItemId == orderItemId)
            .Select(k => k.LicenseKeyHash)
            .ToListAsync(cancellationToken);
        var existingHashSet = existingHashes.ToHashSet(StringComparer.Ordinal);
        var existingCount = existingHashes.Count;

        var attached = 0;
        foreach (var code in deliveredCodes)
        {
            // Never exceed the line's own requested quantity — a defensive backstop on top of
            // SupplierFulfillment's own RequestedQuantity/DeliveredQuantity invariants.
            if (existingCount + attached >= item.Quantity)
            {
                logger.LogWarning(
                    "Supplier delivered more codes than order item {OrderItemId} requires — surplus code(s) discarded, not attached.",
                    orderItemId);
                break;
            }

            var hash = OrderLicenseKey.Hash(code);
            if (!existingHashSet.Add(hash))
            {
                // Idempotency backstop: this exact code was already attached (e.g. a duplicate
                // delivery notification) — never attach the same redemption code twice.
                continue;
            }

            commerceDb.OrderLicenseKeys.Add(OrderLicenseKey.Create(orderId, orderItemId, productId, code, item.ProductVariantId));
            attached++;
        }

        if (attached > 0)
        {
            await commerceDb.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Attached {Count} supplier-delivered code(s) to order {OrderId} item {OrderItemId}.",
                attached, orderId, orderItemId);
        }

        // Recomputed fresh (not just existingCount + attached) so this is correct even though the
        // dedup loop above may have skipped some already-seen codes — deliberately conservative.
        var totalDelivered = await commerceDb.OrderLicenseKeys.CountAsync(k => k.OrderItemId == orderItemId, cancellationToken);
        var remaining = item.Quantity - totalDelivered;

        if (remaining > 0 && isScopeExhausted && item.ProductVariantId is Guid variantId)
        {
            await TryManualFallbackAsync(order, item, variantId, cancellationToken);
        }

        // Mirrors OrderFulfillmentService.FulfillMissingAsync's own completion check — supplier
        // delivery (directly, or via the manual fallback just attempted above) can be what finally
        // makes every line fully delivered, and the order must flip to Completed exactly the same way
        // manual delivery already does, never left stuck in Processing.
        if (order.Status is OrderStatus.Pending or OrderStatus.Processing)
        {
            var totalKeys = await commerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == orderId, cancellationToken);
            var required = order.Items.Where(i => i.LineItemType == OrderLineItemType.Product).Sum(i => i.Quantity);

            if (required > 0 && totalKeys >= required)
            {
                order.Complete();
                await commerceDb.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Implements <see cref="FulfillmentMode.SupplierFirst"/>'s manual-fallback rule. The caller
    /// (<c>SupplierFulfillmentService</c>) has already confirmed <c>isScopeExhausted</c> — no further
    /// AUTOMATIC supplier follow-up will ever be created for this exact scope — before this method is
    /// even reached, and this method is only ever reached from a terminal-state notification in the
    /// first place (see <see cref="ISupplierFulfillmentDeliverySink"/>), so neither the first
    /// <c>PartialFailed</c> nor anything still <c>Unknown</c>/<c>Submitting</c>/<c>Submitted</c> can
    /// ever reach here. A no-op for every mode except <see cref="FulfillmentMode.SupplierFirst"/> —
    /// <see cref="FulfillmentMode.SupplierOnly"/> specifically must never touch manual inventory, full
    /// stop, regardless of how the supplier attempt resolved. Cross-schema (Catalog reservation +
    /// Commerce license key) so it runs inside <see cref="ICommerceTransactionService"/>, mirroring
    /// checkout's own established pattern for the same class of atomic write — never inside any other
    /// open transaction.
    /// </summary>
    private async Task TryManualFallbackAsync(Order order, OrderItem item, Guid variantId, CancellationToken cancellationToken)
    {
        var variant = await catalogDb.ProductVariants.AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(v => new { v.FulfillmentMode })
            .FirstOrDefaultAsync(cancellationToken);

        if (variant is null || variant.FulfillmentMode != FulfillmentMode.SupplierFirst)
        {
            return;
        }

        await transactionService.ExecuteAsync(async ct =>
        {
            // Recomputed fresh, inside the transaction, in case anything else covered part of this
            // shortfall since the caller last checked — never trusts a count carried in from outside.
            var currentCount = await commerceDb.OrderLicenseKeys.CountAsync(k => k.OrderItemId == item.Id, ct);
            var stillNeeded = item.Quantity - currentCount;
            if (stillNeeded <= 0)
            {
                return;
            }

            var reserved = await inventoryEngine.ReservePartialCodesAsync(variantId, stillNeeded, userId: null, cartId: null, ct);
            if (reserved.Count == 0)
            {
                logger.LogWarning(
                    "FulfillmentRouting: SupplierFirst exhausted for order {OrderId} item {OrderItemId} but no manual stock is available either — {Remaining} unit(s) remain undelivered, needs manual attention.",
                    order.Id, item.Id, stillNeeded);
                return;
            }

            var assignments = reserved.Select(r => (item.Id, r.CodeId)).ToList();
            var committed = await inventoryEngine.CommitReservationsAsync(order.Id, assignments, ct);

            foreach (var code in committed)
            {
                commerceDb.OrderLicenseKeys.Add(OrderLicenseKey.Create(order.Id, item.Id, item.ProductId!.Value, code.DigitalCode, variantId, code.CodeId));
            }

            await commerceDb.SaveChangesAsync(ct);
            await catalogDb.SaveChangesAsync(ct);

            logger.LogInformation(
                "FulfillmentRouting: SupplierFirst exhausted for order {OrderId} item {OrderItemId} — manual fallback covered {Delivered} of {Needed} remaining unit(s).",
                order.Id, item.Id, committed.Count, stillNeeded);
        }, cancellationToken);
    }
}
