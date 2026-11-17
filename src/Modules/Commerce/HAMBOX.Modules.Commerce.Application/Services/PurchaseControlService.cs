using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Reservation / purchase abuse controls (audit R22): per-product per-order and per-customer-per-day caps, plus the
/// system flags that park a paid order for an admin instead of delivering it automatically — sensitive products and
/// suspicious activity (many orders or many failed payments in a short window).
/// </summary>
public sealed class PurchaseControlService(
    ICommerceDbContext commerceDb,
    HAMBOX.Application.Abstractions.IPlatformSettingsProvider? platformSettings = null,
    IMembershipAccessProvider? membership = null)
{
    /// <summary>This many orders (any state but cancelled/failed) created in the last hour trips the velocity flag.</summary>
    public const int VelocityOrdersPerHour = 5;

    /// <summary>This many failed payments in the last 24 hours trips the failed-payment flag.</summary>
    public const int FailedPaymentsPerDay = 4;

    /// <summary>Checks every cart line against its product's caps. Returns the first violation.</summary>
    public async Task<Result> CheckLimitsAsync(
        string userId,
        IEnumerable<CartItem> items,
        IReadOnlyDictionary<Guid, Product> products,
        CancellationToken cancellationToken)
    {
        var requestedByProduct = items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        List<Guid>? dailyLimited = null;
        bool? isMember = null;

        foreach (var (productId, requested) in requestedByProduct)
        {
            if (!products.TryGetValue(productId, out var product))
            {
                continue;
            }

            // A product can exempt customers with an active membership (Elite) from its caps.
            if (product.ExemptMembersFromPurchaseLimits && (isMember ??= await IsMemberAsync(userId, cancellationToken)))
            {
                continue;
            }

            if (product.MaxQuantityPerOrder is int perOrder && requested > perOrder)
            {
                return Result.Failure(CommerceErrors.PurchaseLimitPerOrderExceeded(product.NameEn, perOrder));
            }

            if (product.MaxQuantityPerUserPerDay is not null)
            {
                (dailyLimited ??= []).Add(productId);
            }
        }

        if (dailyLimited is null)
        {
            return Result.Success();
        }

        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var bought = await commerceDb.OrderItems
            .Where(i => i.ProductId != null && dailyLimited.Contains(i.ProductId.Value))
            .Join(
                commerceDb.Orders.Where(o => o.UserId == userId
                    && o.CreatedOnUtc >= since
                    && o.Status != OrderStatus.Cancelled
                    && o.Status != OrderStatus.Failed
                    && o.PaymentStatus != PaymentStatus.Failed),
                i => i.OrderId,
                o => o.Id,
                (i, o) => new { ProductId = i.ProductId!.Value, i.Quantity })
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Total, cancellationToken);

        foreach (var productId in dailyLimited)
        {
            var product = products[productId];
            var limit = product.MaxQuantityPerUserPerDay!.Value;
            var alreadyBought = bought.GetValueOrDefault(productId);
            if (alreadyBought + requestedByProduct[productId] > limit)
            {
                return Result.Failure(CommerceErrors.PurchaseLimitPerDayExceeded(product.NameEn, limit, alreadyBought));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Why <paramref name="order"/> should wait for manual review, or <c>null</c> when it can be delivered automatically.
    /// Pure assessment — the caller decides to <see cref="Order.PlaceOnHold"/>.
    /// </summary>
    public async Task<string?> AssessAsync(
        Order order,
        IReadOnlyDictionary<Guid, Product> products,
        CancellationToken cancellationToken)
    {
        var isMember = order.Items.Any(i => i.ProductId is Guid pid && products.TryGetValue(pid, out var p) && p.RequiresManualReview && p.ExemptMembersFromPurchaseLimits)
            && await IsMemberAsync(order.UserId, cancellationToken);
        var sensitive = order.Items
            .Where(i => i.ProductId is Guid pid && products.TryGetValue(pid, out var p) && p.RequiresManualReview
                && !(isMember && p.ExemptMembersFromPurchaseLimits))
            .Select(i => products[i.ProductId!.Value].NameEn)
            .Distinct()
            .ToList();

        if (sensitive.Count > 0)
        {
            return $"Sensitive product: {string.Join(", ", sensitive)}";
        }

        var lastHour = DateTimeOffset.UtcNow.AddHours(-1);
        var recentOrders = await commerceDb.Orders.CountAsync(
            o => o.UserId == order.UserId
                && o.Id != order.Id
                && o.CreatedOnUtc >= lastHour
                && o.Status != OrderStatus.Cancelled
                && o.Status != OrderStatus.Failed,
            cancellationToken);

        var (velocityLimit, failedLimit) = await ResolveRiskLimitsAsync(cancellationToken);

        if (recentOrders + 1 >= velocityLimit)
        {
            return $"Suspicious activity: {recentOrders + 1} orders in the last hour";
        }

        var lastDay = DateTimeOffset.UtcNow.AddHours(-24);
        var failedPayments = await commerceDb.Orders.CountAsync(
            o => o.UserId == order.UserId
                && o.CreatedOnUtc >= lastDay
                && (o.Status == OrderStatus.Failed || o.PaymentStatus == PaymentStatus.Failed),
            cancellationToken);

        if (failedPayments >= failedLimit)
        {
            return $"Suspicious activity: {failedPayments} failed payments in the last 24 hours";
        }

        return null;
    }

    /// <summary>Assesses and, when flagged, parks the order. Returns the reason, or null when nothing was flagged.</summary>
    public async Task<string?> ApplyAsync(
        Order order,
        IReadOnlyDictionary<Guid, Product> products,
        CancellationToken cancellationToken)
    {
        var reason = await AssessAsync(order, products, cancellationToken);
        if (reason is not null)
        {
            order.PlaceOnHold(OrderHoldKind.ManualReview, reason);
        }

        return reason;
    }

    /// <summary>True for a customer with an active, bought membership (a fallback to the free default plan is not one).</summary>
    private async Task<bool> IsMemberAsync(string? userId, CancellationToken cancellationToken)
    {
        if (membership is null || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        try
        {
            var access = await membership.GetAccessInfoAsync(userId, cancellationToken);
            return access.HasActiveMembership && access.SubscriptionId is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The risk thresholds are platform settings (Commerce → purchase limits); the constants are the fallback.</summary>
    private async Task<(int VelocityPerHour, int FailedPerDay)> ResolveRiskLimitsAsync(CancellationToken cancellationToken)
    {
        if (platformSettings is null)
        {
            return (VelocityOrdersPerHour, FailedPaymentsPerDay);
        }

        try
        {
            var commerce = await platformSettings.GetAsync<HAMBOX.Application.PlatformSettings.CommerceSettingsPayload>(
                HAMBOX.Application.PlatformSettings.PlatformSettingsCategoryKeys.Commerce, cancellationToken);
            return (
                commerce.VelocityOrdersPerHour > 0 ? commerce.VelocityOrdersPerHour : VelocityOrdersPerHour,
                commerce.FailedPaymentsPerDay > 0 ? commerce.FailedPaymentsPerDay : FailedPaymentsPerDay);
        }
        catch (Exception)
        {
            return (VelocityOrdersPerHour, FailedPaymentsPerDay);
        }
    }
}
