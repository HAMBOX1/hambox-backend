using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

public sealed record OrderFulfillmentResult(int CodesDelivered, bool OrderCompleted);

public sealed class OrderFulfillmentService
{
    private readonly ICommerceDbContext _commerceDb;
    private readonly IInventoryEngine _inventoryEngine;

    public OrderFulfillmentService(ICommerceDbContext commerceDb, IInventoryEngine inventoryEngine)
    {
        _commerceDb = commerceDb;
        _inventoryEngine = inventoryEngine;
    }

    public async Task<OrderFulfillmentResult> FulfillMissingAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Kind == OrderKind.Membership)
        {
            return new OrderFulfillmentResult(0, false);
        }

        if (order.PaymentStatus != PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Only paid orders can be fulfilled.");
        }

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.Failed)
        {
            throw new InvalidOperationException("Cancelled, refunded, or failed orders cannot be fulfilled.");
        }

        var existingKeys = await _commerceDb.OrderLicenseKeys
            .Where(k => k.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var keysByItem = existingKeys.GroupBy(k => k.OrderItemId).ToDictionary(g => g.Key, g => g.Count());
        var delivered = 0;

        await _inventoryEngine.ExpireStaleReservationsAsync(cancellationToken);

        foreach (var item in order.Items.Where(i => i.LineItemType == OrderLineItemType.Product && i.ProductId is Guid))
        {
            keysByItem.TryGetValue(item.Id, out var existingCount);
            var missing = item.Quantity - existingCount;
            if (missing <= 0)
            {
                continue;
            }

            if (item.ProductVariantId is Guid variantId)
            {
                var reserved = await _inventoryEngine.ReserveCodesAsync(
                    variantId,
                    missing,
                    order.UserId,
                    cartId: null,
                    cancellationToken);

                var assignments = reserved
                    .Select((code, index) => (item.Id, code.CodeId))
                    .ToList();

                var committed = await _inventoryEngine.CommitReservationsAsync(
                    order.Id,
                    assignments,
                    cancellationToken);

                foreach (var code in committed)
                {
                    _commerceDb.OrderLicenseKeys.Add(OrderLicenseKey.Create(
                        order.Id,
                        item.Id,
                        item.ProductId!.Value,
                        code.DigitalCode,
                        item.ProductVariantId,
                        code.CodeId));
                    delivered++;
                }
            }

            // Legacy order lines with no ProductVariantId predate the fix that requires a real,
            // inventory-backed variant at checkout. There is no genuine deliverable to auto-assign
            // here — skip rather than fabricate a license key; an admin must resolve these via
            // AssignManualCodeAsync with a real code.
        }

        var orderCompleted = false;
        if (delivered > 0 && order.Status is OrderStatus.Pending or OrderStatus.Processing)
        {
            var allKeys = await _commerceDb.OrderLicenseKeys
                .Where(k => k.OrderId == order.Id)
                .ToListAsync(cancellationToken);

            var required = order.Items
                .Where(i => i.LineItemType == OrderLineItemType.Product)
                .Sum(i => i.Quantity);

            if (allKeys.Count >= required && required > 0)
            {
                if (order.Status == OrderStatus.Processing || order.Status == OrderStatus.Pending)
                {
                    order.Complete();
                    orderCompleted = true;
                }
            }
            else if (order.Status == OrderStatus.Pending)
            {
                order.MarkProcessing();
            }
        }

        return new OrderFulfillmentResult(delivered, orderCompleted);
    }

    public async Task<OrderLicenseKey> AssignManualCodeAsync(
        Order order,
        Guid orderItemId,
        string licenseKey,
        CancellationToken cancellationToken)
    {
        if (order.PaymentStatus != PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Manual codes can only be assigned to paid orders.");
        }

        var item = order.Items.FirstOrDefault(i => i.Id == orderItemId)
            ?? throw new InvalidOperationException("Order item was not found.");

        if (item.LineItemType != OrderLineItemType.Product || item.ProductId is not Guid productId)
        {
            throw new InvalidOperationException("Manual codes can only be assigned to product line items.");
        }

        var existingCount = await _commerceDb.OrderLicenseKeys
            .CountAsync(k => k.OrderItemId == orderItemId, cancellationToken);

        if (existingCount >= item.Quantity)
        {
            throw new InvalidOperationException("This line item already has the required number of codes.");
        }

        var key = OrderLicenseKey.Create(
            order.Id,
            orderItemId,
            productId,
            licenseKey.Trim(),
            item.ProductVariantId);

        _commerceDb.OrderLicenseKeys.Add(key);
        return key;
    }
}
