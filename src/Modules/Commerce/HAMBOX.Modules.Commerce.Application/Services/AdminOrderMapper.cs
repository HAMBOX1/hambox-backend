using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Services;

public static class AdminOrderMapper
{
    public static string ResolveCustomerName(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Customer";
        }

        var local = email.Split('@')[0];
        var parts = local
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return email;
        }

        return string.Join(' ', parts.Select(static p =>
            p.Length == 1
                ? char.ToUpperInvariant(p[0]).ToString()
                : char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }

    public static string ResolveOrderStatusLabel(Order order) =>
        order.Status switch
        {
            OrderStatus.Processing => "Processing",
            OrderStatus.Completed => "Completed",
            OrderStatus.Cancelled => "Cancelled",
            OrderStatus.Refunded => "Refunded",
            OrderStatus.Failed => "Failed",
            OrderStatus.Pending when order.PaymentStatus == PaymentStatus.Paid => "Processing",
            OrderStatus.Pending when order.PaymentStatus == PaymentStatus.Failed => "Failed",
            _ => "Pending",
        };

    public static string ResolvePaymentStatusLabel(PaymentStatus status) =>
        status switch
        {
            PaymentStatus.Paid => "Paid",
            PaymentStatus.Failed => "Failed",
            PaymentStatus.Refunded => "Refunded",
            _ => "Pending",
        };

    public static string ResolveDeliveryStatus(
        Order order,
        IReadOnlyList<OrderLicenseKey> licenseKeys)
    {
        if (order.Items.Count == 0)
        {
            return "Awaiting Delivery";
        }

        var deliveredCount = licenseKeys.Count(k => !string.IsNullOrWhiteSpace(k.LicenseKey));
        if (deliveredCount == 0)
        {
            return "Awaiting Delivery";
        }

        var required = order.Items.Sum(i => i.Quantity);
        if (deliveredCount >= required && order.Status == OrderStatus.Completed)
        {
            return "Delivered";
        }

        return "Partially Delivered";
    }

    public static string ResolveItemDeliveryStatus(int quantity, int deliveredCodes) =>
        deliveredCodes switch
        {
            0 => "Awaiting Delivery",
            var count when count >= quantity => "Delivered",
            _ => "Partially Delivered",
        };

    public static string MaskLicenseKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "—";
        }

        if (key.Length <= 8)
        {
            return new string('•', key.Length);
        }

        return $"{key[..4]}{new string('•', Math.Max(4, key.Length - 8))}{key[^4..]}";
    }

    public static decimal ResolveMembershipDiscount(IReadOnlyList<OrderAppliedPromotion> promotions) =>
        promotions
            .Where(p => p.PromotionType == PromotionType.Membership)
            .Sum(p => p.DiscountAmount);

    public static string? ResolveCouponCode(IReadOnlyList<OrderAppliedPromotion> promotions) =>
        promotions
            .Select(p => p.CouponCode)
            .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));

    public static AdminOrderListItemDto ToListItem(
        Order order,
        IReadOnlyList<OrderAppliedPromotion> promotions,
        IReadOnlyList<OrderLicenseKey> licenseKeys)
    {
        var productNames = order.Items
            .Select(i => i.ProductNameEn)
            .Distinct()
            .Take(3)
            .ToList();

        var summary = productNames.Count switch
        {
            0 => "—",
            <= 2 => string.Join(", ", productNames),
            _ => $"{string.Join(", ", productNames.Take(2))} +{productNames.Count - 2}",
        };

        return new AdminOrderListItemDto(
            order.Id,
            order.OrderNumber,
            ResolveCustomerName(order.Email),
            order.Email,
            order.Items.Sum(i => i.Quantity),
            summary,
            order.TotalAmount,
            order.PaymentMethod,
            ResolvePaymentStatusLabel(order.PaymentStatus),
            ResolveOrderStatusLabel(order),
            ResolveDeliveryStatus(order, licenseKeys),
            ResolveMembershipDiscount(promotions),
            ResolveCouponCode(promotions),
            order.CreatedOnUtc);
    }

    public static IReadOnlyList<AdminOrderTimelineEventDto> BuildTimeline(
        Order order,
        IReadOnlyList<OrderAuditEntry> auditEntries)
    {
        var events = new List<AdminOrderTimelineEventDto>
        {
            new("OrderCreated", "Order was created.", order.CreatedOnUtc),
        };

        if (order.PaymentStatus == PaymentStatus.Paid || order.PaymentStatus == PaymentStatus.Refunded)
        {
            events.Add(new(
                "PaymentCompleted",
                "Payment was captured successfully.",
                order.CreatedOnUtc.AddMinutes(1)));
        }

        if (order.Status is OrderStatus.Processing or OrderStatus.Completed)
        {
            events.Add(new(
                "InventoryReserved",
                "Inventory was reserved for this order.",
                order.CreatedOnUtc.AddMinutes(1)));
        }

        if (order.Status == OrderStatus.Completed)
        {
            events.Add(new(
                "CodesDelivered",
                "Digital codes were delivered to the customer.",
                order.ModifiedOnUtc ?? order.CreatedOnUtc.AddMinutes(2)));

            events.Add(new(
                "Completed",
                "Order was marked as completed.",
                order.ModifiedOnUtc ?? order.CreatedOnUtc.AddMinutes(2)));
        }

        if (order.Status == OrderStatus.Refunded || order.PaymentStatus == PaymentStatus.Refunded)
        {
            events.Add(new(
                "RefundIssued",
                "A refund was issued for this order.",
                order.ModifiedOnUtc ?? order.CreatedOnUtc.AddMinutes(3)));
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            events.Add(new(
                "Cancelled",
                "Order was cancelled.",
                order.ModifiedOnUtc ?? order.CreatedOnUtc.AddMinutes(2)));
        }

        foreach (var entry in auditEntries.OrderBy(e => e.OccurredOnUtc))
        {
            events.Add(new(entry.EventType, entry.Description, entry.OccurredOnUtc));
        }

        return events
            .OrderBy(e => e.OccurredOnUtc)
            .ToList();
    }
}
