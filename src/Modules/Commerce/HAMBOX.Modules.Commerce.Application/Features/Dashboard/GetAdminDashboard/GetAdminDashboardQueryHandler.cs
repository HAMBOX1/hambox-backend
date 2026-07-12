using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Dashboard;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Dashboard.GetAdminDashboard;

public sealed record GetAdminDashboardQuery() : IRequest<Result<AdminDashboardDto>>;

internal sealed class GetAdminDashboardQueryHandler
    : IRequestHandler<GetAdminDashboardQuery, Result<AdminDashboardDto>>
{
    private const int RecentOrderLimit = 8;
    private const int RecentCustomerLimit = 8;
    private const int LowStockItemLimit = 8;

    private readonly ICommerceDbContext _commerceDb;
    private readonly ICatalogDbContext _catalogDb;
    private readonly IInventoryEngine _inventoryEngine;

    public GetAdminDashboardQueryHandler(
        ICommerceDbContext commerceDb,
        ICatalogDbContext catalogDb,
        IInventoryEngine inventoryEngine)
    {
        _commerceDb = commerceDb;
        _catalogDb = catalogDb;
        _inventoryEngine = inventoryEngine;
    }

    public async Task<Result<AdminDashboardDto>> Handle(
        GetAdminDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var tomorrowStart = todayStart.AddDays(1);
        var weekAhead = DateTime.UtcNow.AddDays(7);

        var orders = await _commerceDb.Orders.AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        var licenseKeys = await _commerceDb.OrderLicenseKeys.AsNoTracking()
            .ToListAsync(cancellationToken);

        var keysByOrder = licenseKeys
            .GroupBy(k => k.OrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderLicenseKey>)g.ToList());

        var todaysOrders = orders.Count(o => o.CreatedOnUtc >= todayStart && o.CreatedOnUtc < tomorrowStart);
        var pendingOrders = orders.Count(o =>
            o.Status is OrderStatus.Pending or OrderStatus.Processing);
        var failedOrders = orders.Count(o =>
            o.Status == OrderStatus.Failed || o.PaymentStatus == PaymentStatus.Failed);
        var manualReviewOrders = orders.Count(o =>
            o.PaymentStatus == PaymentStatus.Paid &&
            o.Status is OrderStatus.Processing or OrderStatus.Pending &&
            !IsFullyDelivered(o, keysByOrder));

        var completedToday = orders
            .Where(o => o.Status == OrderStatus.Completed && o.CreatedOnUtc >= todayStart && o.CreatedOnUtc < tomorrowStart)
            .ToList();

        var revenueToday = completedToday.Sum(o => o.TotalAmount);
        var totalRevenue = orders
            .Where(o => o.Status == OrderStatus.Completed)
            .Sum(o => o.TotalAmount);
        var averageOrderValue = completedToday.Count > 0
            ? Math.Round(revenueToday / completedToday.Count, 2)
            : 0m;
        var refunds = orders.Count(o =>
            o.Status == OrderStatus.Refunded || o.PaymentStatus == PaymentStatus.Refunded);
        var failedPayments = orders.Count(o => o.PaymentStatus == PaymentStatus.Failed);

        var orderMetrics = new AdminDashboardOrderMetricsDto(
            todaysOrders,
            pendingOrders,
            failedOrders,
            manualReviewOrders,
            revenueToday,
            totalRevenue,
            averageOrderValue,
            refunds);

        var inventoryStats = await _inventoryEngine.GetStatisticsAsync(cancellationToken: cancellationToken);
        var inventoryMetrics = new AdminDashboardInventoryMetricsDto(
            inventoryStats.LowStockVariants,
            inventoryStats.OutOfStockVariants,
            inventoryStats.Available,
            inventoryStats.InventoryValue);

        var activePromotions = await _commerceDb.Promotions.AsNoTracking()
            .CountAsync(p => p.Status == PromotionStatus.Active, cancellationToken);
        var activeCoupons = await _commerceDb.CouponCodes.AsNoTracking()
            .CountAsync(c => c.IsActive, cancellationToken);
        var redemptionsToday = await _commerceDb.PromotionRedemptions.AsNoTracking()
            .CountAsync(
                r => r.RedeemedOnUtc >= todayStart.UtcDateTime && r.RedeemedOnUtc < tomorrowStart.UtcDateTime,
                cancellationToken);

        var promotionMetrics = new AdminDashboardPromotionMetricsDto(
            activePromotions,
            activeCoupons,
            redemptionsToday);

        var activeSubscriptions = await _commerceDb.MembershipSubscriptions.AsNoTracking()
            .CountAsync(s => s.Status == MembershipSubscriptionStatus.Active, cancellationToken);
        var expiringWithinWeek = await _commerceDb.MembershipSubscriptions.AsNoTracking()
            .CountAsync(
                s => s.Status == MembershipSubscriptionStatus.Active && s.ExpiresOnUtc <= weekAhead,
                cancellationToken);
        var mrr = await _commerceDb.MembershipPlans.AsNoTracking()
            .Where(p => p.Status == MembershipPlanStatus.Active)
            .Join(
                _commerceDb.MembershipSubscriptions.Where(s => s.Status == MembershipSubscriptionStatus.Active),
                p => p.Id,
                s => s.PlanId,
                (p, _) => p.Price)
            .SumAsync(cancellationToken);

        var membershipMetrics = new AdminDashboardMembershipMetricsDto(
            activeSubscriptions,
            expiringWithinWeek,
            mrr);

        var alerts = new AdminDashboardAlertsDto(
            failedOrders,
            failedPayments,
            inventoryStats.LowStockVariants,
            inventoryStats.OutOfStockVariants);

        var recentOrders = orders
            .Take(RecentOrderLimit)
            .Select(o => MapRecentOrder(o, keysByOrder))
            .ToList();

        var recentCustomers = orders
            .GroupBy(o => o.Email.ToLowerInvariant())
            .Select(g =>
            {
                var latest = g.OrderByDescending(o => o.CreatedOnUtc).First();
                return new AdminDashboardRecentCustomerDto(
                    latest.Email,
                    AdminOrderMapper.ResolveCustomerName(latest.Email),
                    g.Count(),
                    g.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount),
                    latest.CreatedOnUtc);
            })
            .OrderByDescending(c => c.LastOrderDate)
            .Take(RecentCustomerLimit)
            .ToList();

        var lowStockItems = await BuildLowStockItemsAsync(cancellationToken);

        return Result.Success(new AdminDashboardDto(
            orderMetrics,
            inventoryMetrics,
            promotionMetrics,
            membershipMetrics,
            alerts,
            recentOrders,
            recentCustomers,
            lowStockItems));
    }

    private static bool IsFullyDelivered(
        Order order,
        IReadOnlyDictionary<Guid, IReadOnlyList<OrderLicenseKey>> keysByOrder)
    {
        keysByOrder.TryGetValue(order.Id, out var keys);
        keys ??= [];
        var deliveryStatus = AdminOrderMapper.ResolveDeliveryStatus(order, keys);
        return deliveryStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase);
    }

    private static AdminDashboardRecentOrderDto MapRecentOrder(
        Order order,
        IReadOnlyDictionary<Guid, IReadOnlyList<OrderLicenseKey>> keysByOrder)
    {
        keysByOrder.TryGetValue(order.Id, out var keys);
        keys ??= [];

        return new AdminDashboardRecentOrderDto(
            order.Id,
            order.OrderNumber,
            AdminOrderMapper.ResolveCustomerName(order.Email),
            order.Email,
            order.TotalAmount,
            AdminOrderMapper.ResolveOrderStatusLabel(order),
            AdminOrderMapper.ResolvePaymentStatusLabel(order.PaymentStatus),
            AdminOrderMapper.ResolveDeliveryStatus(order, keys),
            order.CreatedOnUtc);
    }

    private async Task<IReadOnlyList<AdminDashboardLowStockItemDto>> BuildLowStockItemsAsync(
        CancellationToken cancellationToken)
    {
        var variants = await _catalogDb.ProductVariants.AsNoTracking()
            .Where(v => v.IsVisible && v.Status == ProductVariantStatus.Active)
            .Select(v => new { v.Id, v.ProductId, v.Sku, v.LowStockThreshold })
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
        {
            return [];
        }

        var variantIds = variants.Select(v => v.Id).ToList();
        var stockMap = await _inventoryEngine.GetVariantStockBulkAsync(variantIds, cancellationToken);

        var lowStockVariantIds = variants
            .Where(v => stockMap.TryGetValue(v.Id, out var stock) && (stock.IsLowStock || stock.IsOutOfStock))
            .Select(v => v.Id)
            .Take(LowStockItemLimit * 3)
            .ToList();

        if (lowStockVariantIds.Count == 0)
        {
            return [];
        }

        var productIds = variants
            .Where(v => lowStockVariantIds.Contains(v.Id))
            .Select(v => v.ProductId)
            .Distinct()
            .ToList();

        var productNames = await _catalogDb.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NameEn })
            .ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);

        return variants
            .Where(v => lowStockVariantIds.Contains(v.Id))
            .Select(v =>
            {
                stockMap.TryGetValue(v.Id, out var stock);
                productNames.TryGetValue(v.ProductId, out var productName);
                return new AdminDashboardLowStockItemDto(
                    v.Id,
                    v.ProductId,
                    productName ?? "Product",
                    v.Sku,
                    stock?.Available ?? 0,
                    v.LowStockThreshold,
                    stock?.IsOutOfStock ?? true);
            })
            .OrderBy(i => i.AvailableStock)
            .Take(LowStockItemLimit)
            .ToList();
    }
}
