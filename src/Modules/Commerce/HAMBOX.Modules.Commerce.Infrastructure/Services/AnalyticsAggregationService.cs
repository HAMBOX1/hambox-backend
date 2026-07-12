using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Analytics;
using HAMBOX.Modules.Commerce.Application.Contracts.Reports;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class AnalyticsAggregationService(
    ICommerceDbContext commerceDb,
    ICatalogDbContext catalogDb,
    IInventoryEngine inventoryEngine,
    IWorkerRuntimeState workerState,
    IReportDocumentGenerator reportDocumentGenerator) : IAnalyticsAggregationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<AnalyticsOverviewDto> GetOverviewAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);
        var (cmpFrom, cmpTo) = ResolveComparison(request);

        var revenue = await AggregateRevenueAsync(request.DateFrom, request.DateTo, cancellationToken);
        var prevRevenue = cmpFrom.HasValue && cmpTo.HasValue
            ? await AggregateRevenueAsync(cmpFrom.Value, cmpTo.Value, cancellationToken)
            : default;

        var orderCounts = await CountOrdersByStatusAsync(request.DateFrom, request.DateTo, cancellationToken);
        var prevOrderTotal = cmpFrom.HasValue && cmpTo.HasValue
            ? await commerceDb.Orders.AsNoTracking()
                .CountAsync(o => o.CreatedOnUtc >= cmpFrom && o.CreatedOnUtc < cmpTo, cancellationToken)
            : 0;

        var customers = await AggregateCustomersAsync(request.DateFrom, request.DateTo, cancellationToken);

        var activeMemberships = await commerceDb.MembershipSubscriptions.AsNoTracking()
            .CountAsync(s => s.Status == MembershipSubscriptionStatus.Active, cancellationToken);

        var mrr = await SumActiveMrrAsync(cancellationToken);

        var redemptionFrom = request.DateFrom.UtcDateTime;
        var redemptionTo = request.DateTo.UtcDateTime;
        var promoAgg = await commerceDb.PromotionRedemptions.AsNoTracking()
            .Where(r => r.RedeemedOnUtc >= redemptionFrom && r.RedeemedOnUtc < redemptionTo)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Discount = g.Sum(x => x.DiscountAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        var invites = await commerceDb.ReferralHistoryEntries.AsNoTracking()
            .CountAsync(r => r.CreatedOnUtc >= request.DateFrom && r.CreatedOnUtc < request.DateTo, cancellationToken);
        var successfulReferrals = await commerceDb.ReferralHistoryEntries.AsNoTracking()
            .CountAsync(
                r => r.CreatedOnUtc >= request.DateFrom
                     && r.CreatedOnUtc < request.DateTo
                     && r.PointsEarned > 0,
                cancellationToken);

        var searchTotal = await catalogDb.SearchQueryLogs.AsNoTracking()
            .CountAsync(s => s.CreatedOnUtc >= request.DateFrom && s.CreatedOnUtc < request.DateTo, cancellationToken);
        var zeroResults = await catalogDb.SearchQueryLogs.AsNoTracking()
            .CountAsync(
                s => s.CreatedOnUtc >= request.DateFrom
                     && s.CreatedOnUtc < request.DateTo
                     && s.ResultCount == 0,
                cancellationToken);
        var productViews = await catalogDb.ProductViewEvents.AsNoTracking()
            .CountAsync(v => v.CreatedOnUtc >= request.DateFrom && v.CreatedOnUtc < request.DateTo, cancellationToken);

        var failedJobs = await commerceDb.OperationalJobs.AsNoTracking()
            .CountAsync(
                j => j.CreatedOnUtc >= request.DateFrom
                     && j.CreatedOnUtc < request.DateTo
                     && j.Status == OperationalJobStatus.Failed,
                cancellationToken);
        var api5xx = await commerceDb.ApiRequestLogs.AsNoTracking()
            .CountAsync(
                l => l.TimestampUtc >= request.DateFrom
                     && l.TimestampUtc < request.DateTo
                     && l.StatusCode >= 500,
                cancellationToken);

        var revenueSeries = await BuildOrderSeriesAsync(
            request.DateFrom,
            request.DateTo,
            o => o.Status == OrderStatus.Completed,
            cancellationToken);
        var ordersSeries = await BuildOrderSeriesAsync(
            request.DateFrom,
            request.DateTo,
            _ => true,
            cancellationToken,
            countOnly: true);

        var totalOrders = orderCounts.Values.Sum();
        var completed = orderCounts.GetValueOrDefault(OrderStatus.Completed);
        var conversion = totalOrders > 0 ? Math.Round((decimal)completed / totalOrders * 100m, 2) : 0m;

        return new AnalyticsOverviewDto(
            period,
            revenue.Gross,
            revenue.Net,
            revenue.Pending,
            revenue.Refunded,
            revenue.Aov,
            totalOrders,
            completed,
            conversion,
            customers.NewCustomers,
            customers.ReturningCustomers,
            activeMemberships,
            mrr,
            promoAgg?.Count ?? 0,
            promoAgg?.Discount ?? 0m,
            invites,
            successfulReferrals,
            searchTotal,
            zeroResults,
            productViews,
            failedJobs,
            api5xx,
            BuildGrowth(revenue.Gross, prevRevenue.Gross),
            BuildGrowth(totalOrders, prevOrderTotal),
            revenueSeries,
            ordersSeries);
    }

    public async Task<AnalyticsRevenueDto> GetRevenueAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);
        var (cmpFrom, cmpTo) = ResolveComparison(request);

        var revenue = await AggregateRevenueAsync(request.DateFrom, request.DateTo, cancellationToken);
        var prev = cmpFrom.HasValue && cmpTo.HasValue
            ? await AggregateRevenueAsync(cmpFrom.Value, cmpTo.Value, cancellationToken)
            : default;

        var membershipRevenue = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.CreatedOnUtc >= request.DateFrom
                        && o.CreatedOnUtc < request.DateTo
                        && o.Status == OrderStatus.Completed
                        && o.Kind == OrderKind.Membership)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var productRevenue = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.CreatedOnUtc >= request.DateFrom
                        && o.CreatedOnUtc < request.DateTo
                        && o.Status == OrderStatus.Completed
                        && o.Kind == OrderKind.Product)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var series = await BuildOrderSeriesAsync(
            request.DateFrom,
            request.DateTo,
            o => o.Status == OrderStatus.Completed,
            cancellationToken);

        var (byCategory, byProduct) = await AggregateProductCategoryRevenueAsync(
            request.DateFrom,
            request.DateTo,
            cancellationToken);

        var byPlan = await AggregateMembershipPlanRevenueAsync(
            request.DateFrom,
            request.DateTo,
            cancellationToken);

        return new AnalyticsRevenueDto(
            period,
            revenue.Gross,
            revenue.Net,
            revenue.Pending,
            revenue.Refunded,
            revenue.Aov,
            BuildGrowth(revenue.Gross, prev.Gross),
            membershipRevenue,
            productRevenue,
            series,
            byCategory,
            byProduct,
            byPlan);
    }

    public async Task<AnalyticsOrdersDto> GetOrdersAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);
        var (cmpFrom, cmpTo) = ResolveComparison(request);

        var counts = await CountOrdersByStatusAsync(request.DateFrom, request.DateTo, cancellationToken);
        var total = counts.Values.Sum();
        var completed = counts.GetValueOrDefault(OrderStatus.Completed);
        var conversion = total > 0 ? Math.Round((decimal)completed / total * 100m, 2) : 0m;

        var prevTotal = cmpFrom.HasValue && cmpTo.HasValue
            ? await commerceDb.Orders.AsNoTracking()
                .CountAsync(o => o.CreatedOnUtc >= cmpFrom && o.CreatedOnUtc < cmpTo, cancellationToken)
            : 0;

        var processingSeconds = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.CreatedOnUtc >= request.DateFrom
                        && o.CreatedOnUtc < request.DateTo
                        && o.Status == OrderStatus.Completed
                        && o.ModifiedOnUtc != null)
            .Select(o => (o.ModifiedOnUtc!.Value - o.CreatedOnUtc).TotalSeconds)
            .ToListAsync(cancellationToken);
        var validProcessing = processingSeconds.Where(s => s >= 0).ToList();
        double? avgProcessing = validProcessing.Count > 0 ? validProcessing.Average() : null;

        var fulfillmentPairs = await (
            from o in commerceDb.Orders.AsNoTracking()
            where o.CreatedOnUtc >= request.DateFrom
                  && o.CreatedOnUtc < request.DateTo
                  && o.Status == OrderStatus.Completed
            join k in commerceDb.OrderLicenseKeys.AsNoTracking() on o.Id equals k.OrderId
            group k by new { o.Id, o.CreatedOnUtc } into g
            select (g.Min(x => x.CreatedOnUtc) - g.Key.CreatedOnUtc).TotalSeconds)
            .ToListAsync(cancellationToken);
        var validFulfillment = fulfillmentPairs.Where(s => s >= 0).ToList();
        double? avgFulfillment = validFulfillment.Count > 0 ? validFulfillment.Average() : null;

        var series = await BuildOrderSeriesAsync(
            request.DateFrom,
            request.DateTo,
            _ => true,
            cancellationToken,
            countOnly: true);

        var byStatus = counts
            .OrderBy(kv => kv.Key)
            .Select(kv => new AnalyticsNamedValueDto(kv.Key.ToString(), kv.Value, kv.Value))
            .ToList();

        return new AnalyticsOrdersDto(
            period,
            total,
            counts.GetValueOrDefault(OrderStatus.Pending),
            counts.GetValueOrDefault(OrderStatus.Processing),
            completed,
            counts.GetValueOrDefault(OrderStatus.Cancelled),
            counts.GetValueOrDefault(OrderStatus.Refunded),
            counts.GetValueOrDefault(OrderStatus.Failed),
            conversion,
            avgProcessing,
            avgFulfillment,
            BuildGrowth(total, prevTotal),
            series,
            byStatus);
    }

    public async Task<AnalyticsProductsDto> GetProductsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);
        var inventoryStats = await inventoryEngine.GetStatisticsAsync(cancellationToken: cancellationToken);

        var soldRows = await (
            from oi in commerceDb.OrderItems.AsNoTracking()
            join o in commerceDb.Orders.AsNoTracking() on oi.OrderId equals o.Id
            where o.Status == OrderStatus.Completed
                  && o.CreatedOnUtc >= request.DateFrom
                  && o.CreatedOnUtc < request.DateTo
                  && oi.ProductId != null
            group oi by oi.ProductId!.Value into g
            select new
            {
                ProductId = g.Key,
                Qty = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal),
                Name = g.Max(x => x.ProductNameEn),
            })
            .ToListAsync(cancellationToken);

        var topByQty = soldRows
            .OrderByDescending(r => r.Qty)
            .Take(20)
            .Select(r => new AnalyticsNamedValueDto(r.Name, r.Qty, r.Qty, r.ProductId))
            .ToList();

        var topByRevenue = soldRows
            .OrderByDescending(r => r.Revenue)
            .Take(20)
            .Select(r => new AnalyticsNamedValueDto(r.Name, r.Revenue, r.Qty, r.ProductId))
            .ToList();

        var worstByQty = soldRows
            .OrderBy(r => r.Qty)
            .ThenBy(r => r.Name)
            .Take(20)
            .Select(r => new AnalyticsNamedValueDto(r.Name, r.Qty, r.Qty, r.ProductId))
            .ToList();

        var viewRows = await catalogDb.ProductViewEvents.AsNoTracking()
            .Where(v => v.CreatedOnUtc >= request.DateFrom && v.CreatedOnUtc < request.DateTo)
            .GroupBy(v => v.ProductId)
            .Select(g => new { ProductId = g.Key, Views = g.Count() })
            .OrderByDescending(x => x.Views)
            .Take(20)
            .ToListAsync(cancellationToken);

        var viewedIds = viewRows.Select(v => v.ProductId).ToList();
        var viewedNames = viewedIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await catalogDb.Products.AsNoTracking()
                .Where(p => viewedIds.Contains(p.Id))
                .Select(p => new { p.Id, p.NameEn })
                .ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);

        var mostViewed = viewRows
            .Select(v => new AnalyticsNamedValueDto(
                viewedNames.GetValueOrDefault(v.ProductId, "Product"),
                v.Views,
                v.Views,
                v.ProductId))
            .ToList();

        var purchasedIds = soldRows.Select(r => r.ProductId).ToHashSet();
        var neverPurchased = await catalogDb.Products.AsNoTracking()
            .Where(p => !p.IsDeleted && !purchasedIds.Contains(p.Id))
            .OrderBy(p => p.NameEn)
            .Take(20)
            .Select(p => new AnalyticsNamedValueDto(p.NameEn, 0m, 0, p.Id))
            .ToListAsync(cancellationToken);

        var soldQty = soldRows.Sum(r => r.Qty);
        var turnover = (decimal)soldQty / Math.Max(inventoryStats.Available, 1);

        return new AnalyticsProductsDto(
            period,
            inventoryStats.OutOfStockVariants,
            inventoryStats.LowStockVariants,
            inventoryStats.InventoryValue,
            Math.Round(turnover, 4),
            topByQty,
            topByRevenue,
            worstByQty,
            mostViewed,
            neverPurchased);
    }

    public async Task<AnalyticsCategoriesDto> GetCategoriesAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);
        var (byCategory, _) = await AggregateProductCategoryRevenueAsync(
            request.DateFrom,
            request.DateTo,
            cancellationToken);

        var byQuantity = await AggregateCategoryQuantityAsync(
            request.DateFrom,
            request.DateTo,
            cancellationToken);

        var series = await BuildOrderSeriesAsync(
            request.DateFrom,
            request.DateTo,
            o => o.Status == OrderStatus.Completed && o.Kind == OrderKind.Product,
            cancellationToken);

        return new AnalyticsCategoriesDto(period, byCategory, byQuantity, series);
    }

    public async Task<AnalyticsCustomersDto> GetCustomersAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);
        var (cmpFrom, cmpTo) = ResolveComparison(request);

        var customers = await AggregateCustomersAsync(request.DateFrom, request.DateTo, cancellationToken);
        var prevCustomers = cmpFrom.HasValue && cmpTo.HasValue
            ? await AggregateCustomersAsync(cmpFrom.Value, cmpTo.Value, cancellationToken)
            : default;

        var ltvRows = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed)
            .GroupBy(o => o.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Ltv = g.Sum(x => x.TotalAmount),
                Orders = g.Count(),
            })
            .OrderByDescending(x => x.Ltv)
            .Take(20)
            .ToListAsync(cancellationToken);

        var ltvValues = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed)
            .GroupBy(o => o.UserId)
            .Select(g => g.Sum(x => x.TotalAmount))
            .ToListAsync(cancellationToken);
        var avgLtv = ltvValues.Count > 0 ? Math.Round(ltvValues.Average(), 2) : 0m;

        var byCountry = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.CreatedOnUtc >= request.DateFrom
                        && o.CreatedOnUtc < request.DateTo
                        && o.Status == OrderStatus.Completed)
            .GroupBy(o => o.Country == null || o.Country == string.Empty ? "Unknown" : o.Country)
            .Select(g => new AnalyticsNamedValueDto(g.Key, g.Sum(x => x.TotalAmount), g.Count()))
            .OrderByDescending(x => x.Value)
            .Take(50)
            .ToListAsync(cancellationToken);

        var firstCompleted = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed)
            .GroupBy(o => o.UserId)
            .Select(g => g.Min(x => x.CreatedOnUtc))
            .Where(d => d >= request.DateFrom && d < request.DateTo)
            .ToListAsync(cancellationToken);

        var newSeries = BuildSeriesFromDates(request.DateFrom, request.DateTo, firstCompleted);

        var topByLtv = ltvRows
            .Select(r => new AnalyticsNamedValueDto(r.UserId, r.Ltv, r.Orders))
            .ToList();

        return new AnalyticsCustomersDto(
            period,
            customers.NewCustomers,
            customers.ReturningCustomers,
            customers.TotalDistinctBuyers,
            Math.Round(avgLtv, 2),
            BuildGrowth(customers.NewCustomers, prevCustomers.NewCustomers),
            topByLtv,
            byCountry,
            newSeries);
    }

    public async Task<AnalyticsMembershipsDto> GetMembershipsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);

        var activeCount = await commerceDb.MembershipSubscriptions.AsNoTracking()
            .CountAsync(s => s.Status == MembershipSubscriptionStatus.Active, cancellationToken);

        var newInPeriod = await commerceDb.MembershipSubscriptions.AsNoTracking()
            .CountAsync(
                s => s.CreatedOnUtc >= request.DateFrom && s.CreatedOnUtc < request.DateTo,
                cancellationToken);

        var cancellations = await commerceDb.MembershipSubscriptions.AsNoTracking()
            .CountAsync(
                s => s.Status == MembershipSubscriptionStatus.Cancelled
                     && s.ModifiedOnUtc != null
                     && s.ModifiedOnUtc >= request.DateFrom
                     && s.ModifiedOnUtc < request.DateTo,
                cancellationToken);

        var renewals = await commerceDb.MembershipSubscriptions.AsNoTracking()
            .CountAsync(
                s => s.Status == MembershipSubscriptionStatus.Active
                     && s.ModifiedOnUtc != null
                     && s.ModifiedOnUtc >= request.DateFrom
                     && s.ModifiedOnUtc < request.DateTo
                     && s.CreatedOnUtc < request.DateFrom,
                cancellationToken);

        var mrr = await SumActiveMrrAsync(cancellationToken);
        var arr = mrr * 12m;

        var membershipRevenue = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.CreatedOnUtc >= request.DateFrom
                        && o.CreatedOnUtc < request.DateTo
                        && o.Status == OrderStatus.Completed
                        && o.Kind == OrderKind.Membership)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var completedTotal = await commerceDb.Orders.AsNoTracking()
            .CountAsync(
                o => o.CreatedOnUtc >= request.DateFrom
                     && o.CreatedOnUtc < request.DateTo
                     && o.Status == OrderStatus.Completed,
                cancellationToken);
        var membershipCompleted = await commerceDb.Orders.AsNoTracking()
            .CountAsync(
                o => o.CreatedOnUtc >= request.DateFrom
                     && o.CreatedOnUtc < request.DateTo
                     && o.Status == OrderStatus.Completed
                     && o.Kind == OrderKind.Membership,
                cancellationToken);
        var conversion = completedTotal > 0
            ? Math.Round((decimal)membershipCompleted / completedTotal * 100m, 2)
            : 0m;

        var revenueByPlan = await AggregateMembershipPlanRevenueAsync(
            request.DateFrom,
            request.DateTo,
            cancellationToken);

        var series = await BuildOrderSeriesAsync(
            request.DateFrom,
            request.DateTo,
            o => o.Status == OrderStatus.Completed && o.Kind == OrderKind.Membership,
            cancellationToken);

        return new AnalyticsMembershipsDto(
            period,
            activeCount,
            newInPeriod,
            renewals,
            cancellations,
            mrr,
            arr,
            conversion,
            membershipRevenue,
            revenueByPlan,
            series);
    }

    public async Task<AnalyticsPromotionsDto> GetPromotionsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);
        var fromDt = request.DateFrom.UtcDateTime;
        var toDt = request.DateTo.UtcDateTime;

        var redemptionAgg = await commerceDb.PromotionRedemptions.AsNoTracking()
            .Where(r => r.RedeemedOnUtc >= fromDt && r.RedeemedOnUtc < toDt)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Discount = g.Sum(x => x.DiscountAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        var redemptions = redemptionAgg?.Count ?? 0;
        var discountTotal = redemptionAgg?.Discount ?? 0m;

        var promoOrderIdsFromApplied = await commerceDb.OrderAppliedPromotions.AsNoTracking()
            .Select(a => a.OrderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var promoOrderIdsFromRedemptions = await commerceDb.PromotionRedemptions.AsNoTracking()
            .Where(r => r.OrderId != null && r.RedeemedOnUtc >= fromDt && r.RedeemedOnUtc < toDt)
            .Select(r => r.OrderId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var promoOrderIds = promoOrderIdsFromApplied
            .Concat(promoOrderIdsFromRedemptions)
            .Distinct()
            .ToList();

        var revenueOnPromo = promoOrderIds.Count == 0
            ? 0m
            : await commerceDb.Orders.AsNoTracking()
                .Where(o => promoOrderIds.Contains(o.Id)
                            && o.Status == OrderStatus.Completed
                            && o.CreatedOnUtc >= request.DateFrom
                            && o.CreatedOnUtc < request.DateTo)
                .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var completedTotal = await commerceDb.Orders.AsNoTracking()
            .CountAsync(
                o => o.CreatedOnUtc >= request.DateFrom
                     && o.CreatedOnUtc < request.DateTo
                     && o.Status == OrderStatus.Completed,
                cancellationToken);

        var promoCompleted = promoOrderIds.Count == 0
            ? 0
            : await commerceDb.Orders.AsNoTracking()
                .CountAsync(
                    o => promoOrderIds.Contains(o.Id)
                         && o.Status == OrderStatus.Completed
                         && o.CreatedOnUtc >= request.DateFrom
                         && o.CreatedOnUtc < request.DateTo,
                    cancellationToken);

        var conversion = completedTotal > 0
            ? Math.Round((decimal)promoCompleted / completedTotal * 100m, 2)
            : 0m;

        decimal? roi = discountTotal > 0
            ? Math.Round((revenueOnPromo - discountTotal) / discountTotal, 4)
            : null;

        var topCouponIds = await commerceDb.PromotionRedemptions.AsNoTracking()
            .Where(r => r.RedeemedOnUtc >= fromDt && r.RedeemedOnUtc < toDt && r.CouponCodeId != null)
            .GroupBy(r => r.CouponCodeId!.Value)
            .Select(g => new { CouponId = g.Key, Count = g.Count(), Discount = g.Sum(x => x.DiscountAmount) })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync(cancellationToken);

        var couponIds = topCouponIds.Select(c => c.CouponId).ToList();
        var couponCodes = couponIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await commerceDb.CouponCodes.AsNoTracking()
                .Where(c => couponIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Code })
                .ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);

        var topCoupons = topCouponIds
            .Select(c => new AnalyticsNamedValueDto(
                couponCodes.GetValueOrDefault(c.CouponId, c.CouponId.ToString()),
                c.Discount,
                c.Count,
                c.CouponId))
            .ToList();

        var topPromos = await commerceDb.PromotionRedemptions.AsNoTracking()
            .Where(r => r.RedeemedOnUtc >= fromDt && r.RedeemedOnUtc < toDt)
            .GroupBy(r => new { r.PromotionId, r.PromotionName })
            .Select(g => new AnalyticsNamedValueDto(
                g.Key.PromotionName,
                g.Sum(x => x.DiscountAmount),
                g.Count(),
                g.Key.PromotionId))
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync(cancellationToken);

        var redemptionDates = await commerceDb.PromotionRedemptions.AsNoTracking()
            .Where(r => r.RedeemedOnUtc >= fromDt && r.RedeemedOnUtc < toDt)
            .Select(r => r.RedeemedOnUtc)
            .ToListAsync(cancellationToken);

        var series = BuildSeriesFromDateTimes(request.DateFrom, request.DateTo, redemptionDates);

        return new AnalyticsPromotionsDto(
            period,
            redemptions,
            discountTotal,
            revenueOnPromo,
            conversion,
            roi,
            topCoupons,
            topPromos,
            series);
    }

    public async Task<AnalyticsReferralsDto> GetReferralsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);
        var (cmpFrom, cmpTo) = ResolveComparison(request);

        var invites = await commerceDb.ReferralHistoryEntries.AsNoTracking()
            .CountAsync(r => r.CreatedOnUtc >= request.DateFrom && r.CreatedOnUtc < request.DateTo, cancellationToken);
        var successful = await commerceDb.ReferralHistoryEntries.AsNoTracking()
            .CountAsync(
                r => r.CreatedOnUtc >= request.DateFrom
                     && r.CreatedOnUtc < request.DateTo
                     && r.PointsEarned > 0,
                cancellationToken);

        var prevInvites = cmpFrom.HasValue && cmpTo.HasValue
            ? await commerceDb.ReferralHistoryEntries.AsNoTracking()
                .CountAsync(r => r.CreatedOnUtc >= cmpFrom && r.CreatedOnUtc < cmpTo, cancellationToken)
            : 0;

        var conversion = invites > 0 ? Math.Round((decimal)successful / invites * 100m, 2) : 0m;

        var referredUserIds = await commerceDb.ReferralHistoryEntries.AsNoTracking()
            .Where(r => r.CreatedOnUtc >= request.DateFrom
                        && r.CreatedOnUtc < request.DateTo
                        && r.PointsEarned > 0)
            .Select(r => r.ReferredUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var attributedRevenue = referredUserIds.Count == 0
            ? 0m
            : await commerceDb.Orders.AsNoTracking()
                .Where(o => referredUserIds.Contains(o.UserId)
                            && o.Status == OrderStatus.Completed
                            && o.CreatedOnUtc >= request.DateFrom
                            && o.CreatedOnUtc < request.DateTo)
                .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var topReferrers = await commerceDb.ReferralHistoryEntries.AsNoTracking()
            .Where(r => r.CreatedOnUtc >= request.DateFrom
                        && r.CreatedOnUtc < request.DateTo
                        && r.PointsEarned > 0)
            .GroupBy(r => r.ReferrerUserId)
            .Select(g => new AnalyticsNamedValueDto(g.Key, g.Count(), g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync(cancellationToken);

        var inviteDates = await commerceDb.ReferralHistoryEntries.AsNoTracking()
            .Where(r => r.CreatedOnUtc >= request.DateFrom && r.CreatedOnUtc < request.DateTo)
            .Select(r => r.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        var series = BuildSeriesFromDates(request.DateFrom, request.DateTo, inviteDates);

        return new AnalyticsReferralsDto(
            period,
            invites,
            successful,
            conversion,
            attributedRevenue,
            BuildGrowth(invites, prevInvites),
            topReferrers,
            series);
    }

    public async Task<AnalyticsSearchDto> GetSearchAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);

        var logs = await catalogDb.SearchQueryLogs.AsNoTracking()
            .Where(s => s.CreatedOnUtc >= request.DateFrom && s.CreatedOnUtc < request.DateTo)
            .Select(s => new { s.Query, s.ResultCount, s.CreatedOnUtc })
            .ToListAsync(cancellationToken);

        var total = logs.Count;
        var zero = logs.Count(l => l.ResultCount == 0);
        var zeroRate = total > 0 ? Math.Round((decimal)zero / total * 100m, 2) : 0m;

        var topTerms = logs
            .GroupBy(l => l.Query, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AnalyticsNamedValueDto(g.Key, g.Count(), g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToList();

        var zeroTerms = logs
            .Where(l => l.ResultCount == 0)
            .GroupBy(l => l.Query, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AnalyticsNamedValueDto(g.Key, g.Count(), g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToList();

        var series = BuildSeriesFromDates(
            request.DateFrom,
            request.DateTo,
            logs.Select(l => l.CreatedOnUtc));

        return new AnalyticsSearchDto(
            period,
            total,
            zero,
            zeroRate,
            0m,
            topTerms,
            zeroTerms,
            series);
    }

    public async Task<AnalyticsOperationsDto> GetOperationsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = BuildPeriod(request);

        var queueSamples = await commerceDb.OperationalJobs.AsNoTracking()
            .Where(j => j.CreatedOnUtc >= request.DateFrom
                        && j.CreatedOnUtc < request.DateTo
                        && j.StartedOnUtc != null)
            .Select(j => (j.StartedOnUtc!.Value - j.CreatedOnUtc).TotalSeconds)
            .ToListAsync(cancellationToken);
        var validQueue = queueSamples.Where(s => s >= 0).ToList();
        double? avgQueue = validQueue.Count > 0 ? validQueue.Average() : null;

        var fulfillmentPairs = await (
            from o in commerceDb.Orders.AsNoTracking()
            where o.CreatedOnUtc >= request.DateFrom && o.CreatedOnUtc < request.DateTo
            join k in commerceDb.OrderLicenseKeys.AsNoTracking() on o.Id equals k.OrderId
            group k by new { o.Id, o.CreatedOnUtc } into g
            select (double)(g.Min(x => x.CreatedOnUtc) - g.Key.CreatedOnUtc).TotalSeconds)
            .ToListAsync(cancellationToken);
        double? avgDelivery = null;
        var validDelivery = fulfillmentPairs.Where(s => s >= 0).Take(500).ToList();
        if (validDelivery.Count > 0)
        {
            avgDelivery = validDelivery.Average();
        }

        var failedJobs = await commerceDb.OperationalJobs.AsNoTracking()
            .CountAsync(
                j => j.CreatedOnUtc >= request.DateFrom
                     && j.CreatedOnUtc < request.DateTo
                     && j.Status == OperationalJobStatus.Failed,
                cancellationToken);

        var retriedJobs = await commerceDb.OperationalJobs.AsNoTracking()
            .Where(j => j.CreatedOnUtc >= request.DateFrom
                        && j.CreatedOnUtc < request.DateTo
                        && j.Attempts > 1)
            .Select(j => j.Status)
            .ToListAsync(cancellationToken);
        var retrySuccessRate = retriedJobs.Count > 0
            ? Math.Round(
                (decimal)retriedJobs.Count(s => s == OperationalJobStatus.Completed) / retriedJobs.Count * 100m,
                2)
            : 0m;

        var inactiveSuppliers = await catalogDb.InventorySuppliers.AsNoTracking()
            .CountAsync(s => !s.IsDeleted && s.Status != SupplierStatus.Active, cancellationToken);

        var api5xx = await commerceDb.ApiRequestLogs.AsNoTracking()
            .CountAsync(
                l => l.TimestampUtc >= request.DateFrom
                     && l.TimestampUtc < request.DateTo
                     && l.StatusCode >= 500,
                cancellationToken);

        double? workerUptime;
        if (workerState.LastHeartbeatUtc.HasValue)
        {
            var age = DateTimeOffset.UtcNow - workerState.LastHeartbeatUtc.Value;
            workerUptime = age.TotalMinutes <= 2 ? 100d : 0d;
        }
        else
        {
            workerUptime = workerState.IsRunning ? 100d : 0d;
        }

        var failedDates = await commerceDb.OperationalJobs.AsNoTracking()
            .Where(j => j.CreatedOnUtc >= request.DateFrom
                        && j.CreatedOnUtc < request.DateTo
                        && j.Status == OperationalJobStatus.Failed)
            .Select(j => j.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        var api5xxDates = await commerceDb.ApiRequestLogs.AsNoTracking()
            .Where(l => l.TimestampUtc >= request.DateFrom
                        && l.TimestampUtc < request.DateTo
                        && l.StatusCode >= 500)
            .Select(l => l.TimestampUtc)
            .ToListAsync(cancellationToken);

        return new AnalyticsOperationsDto(
            period,
            avgQueue,
            avgDelivery,
            failedJobs,
            retrySuccessRate,
            inactiveSuppliers,
            api5xx,
            workerUptime,
            BuildSeriesFromDates(request.DateFrom, request.DateTo, failedDates),
            BuildSeriesFromDates(request.DateFrom, request.DateTo, api5xxDates));
    }

    public async Task<AnalyticsExportDto> ExportAsync(
        string section,
        string format,
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedSection = (section ?? "overview").Trim().ToLowerInvariant();
        var normalizedFormat = (format ?? "json").Trim().ToLowerInvariant();
        if (normalizedFormat is "excel" or "xls")
        {
            normalizedFormat = ReportFormats.Xlsx;
        }

        object dto = normalizedSection switch
        {
            "revenue" => await GetRevenueAsync(request, cancellationToken),
            "orders" => await GetOrdersAsync(request, cancellationToken),
            "products" => await GetProductsAsync(request, cancellationToken),
            "categories" => await GetCategoriesAsync(request, cancellationToken),
            "customers" => await GetCustomersAsync(request, cancellationToken),
            "memberships" => await GetMembershipsAsync(request, cancellationToken),
            "promotions" => await GetPromotionsAsync(request, cancellationToken),
            "referrals" => await GetReferralsAsync(request, cancellationToken),
            "search" => await GetSearchAsync(request, cancellationToken),
            "operations" => await GetOperationsAsync(request, cancellationToken),
            _ => await GetOverviewAsync(request, cancellationToken),
        };

        var model = BuildReportModelFromAnalytics(normalizedSection, request, dto);
        var document = await reportDocumentGenerator.GenerateAsync(
            new ReportDocumentRequest(model, normalizedFormat),
            cancellationToken);

        return new AnalyticsExportDto(document.FileName, document.ContentType, document.Content);
    }

    private static ReportModel BuildReportModelFromAnalytics(
        string section,
        AnalyticsPeriodRequest request,
        object dto)
    {
        var rows = FlattenDto(dto);
        var table = new ReportTableDto(
            "Metrics",
            [new("metric", "Metric"), new("value", "Value")],
            rows.Select(r => new ReportTableRowDto(new Dictionary<string, string>
            {
                ["metric"] = r.Key,
                ["value"] = r.Value,
            })).ToList());

        return new ReportModel(
            section,
            $"Analytics — {section}",
            "HAMBOX",
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>
            {
                ["From"] = request.DateFrom.ToString("u", CultureInfo.InvariantCulture),
                ["To"] = request.DateTo.ToString("u", CultureInfo.InvariantCulture),
                ["Section"] = section,
            },
            [new ReportSectionDto("Export", Table: table)]);
    }

    private static (DateTimeOffset? from, DateTimeOffset? to) ResolveComparison(AnalyticsPeriodRequest request)
    {
        return request.ComparisonMode switch
        {
            AnalyticsComparisonMode.PreviousPeriod => (
                request.DateFrom - (request.DateTo - request.DateFrom),
                request.DateFrom),
            AnalyticsComparisonMode.PreviousMonth => (
                request.DateFrom.AddMonths(-1),
                request.DateTo.AddMonths(-1)),
            AnalyticsComparisonMode.PreviousYear => (
                request.DateFrom.AddYears(-1),
                request.DateTo.AddYears(-1)),
            _ => (null, null),
        };
    }

    private AnalyticsPeriodDto BuildPeriod(AnalyticsPeriodRequest request, string preset = "Custom")
    {
        var (cmpFrom, cmpTo) = ResolveComparison(request);
        return new AnalyticsPeriodDto(
            request.DateFrom,
            request.DateTo,
            preset,
            request.ComparisonMode.ToString(),
            cmpFrom,
            cmpTo);
    }

    private static AnalyticsGrowthDto BuildGrowth(decimal current, decimal previous) =>
        new(current, previous, PercentChange(current, previous));

    private static AnalyticsGrowthDto BuildGrowth(int current, int previous) =>
        new(current, previous, PercentChange(current, previous));

    private static decimal? PercentChange(decimal current, decimal previous)
    {
        if (previous == 0m)
        {
            return current == 0m ? 0m : null;
        }

        return Math.Round((current - previous) / previous * 100m, 2);
    }

    private async Task<RevenueAgg> AggregateRevenueAsync(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        CancellationToken cancellationToken)
    {
        var rows = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.CreatedOnUtc >= dateFrom && o.CreatedOnUtc < dateTo)
            .Select(o => new { o.Status, o.PaymentStatus, o.TotalAmount })
            .ToListAsync(cancellationToken);

        var gross = rows.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount);
        var refunded = rows.Where(o => o.Status == OrderStatus.Refunded).Sum(o => o.TotalAmount);
        var pending = rows
            .Where(o => o.PaymentStatus == PaymentStatus.Pending
                        || o.Status is OrderStatus.Pending or OrderStatus.Processing)
            .Sum(o => o.TotalAmount);
        var completedCount = rows.Count(o => o.Status == OrderStatus.Completed);
        var aov = completedCount > 0 ? Math.Round(gross / completedCount, 2) : 0m;

        return new RevenueAgg(gross, gross - refunded, pending, refunded, aov);
    }

    private async Task<Dictionary<OrderStatus, int>> CountOrdersByStatusAsync(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        CancellationToken cancellationToken)
    {
        var groups = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.CreatedOnUtc >= dateFrom && o.CreatedOnUtc < dateTo)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return groups.ToDictionary(g => g.Status, g => g.Count);
    }

    private async Task<CustomerAgg> AggregateCustomersAsync(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        CancellationToken cancellationToken)
    {
        var firstOrders = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed)
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, FirstOn = g.Min(x => x.CreatedOnUtc) })
            .ToListAsync(cancellationToken);

        var newCustomers = firstOrders.Count(f => f.FirstOn >= dateFrom && f.FirstOn < dateTo);

        var buyersInPeriod = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed
                        && o.CreatedOnUtc >= dateFrom
                        && o.CreatedOnUtc < dateTo)
            .Select(o => o.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var firstLookup = firstOrders.ToDictionary(f => f.UserId, f => f.FirstOn);
        var returning = buyersInPeriod.Count(uid =>
            firstLookup.TryGetValue(uid, out var first) && first < dateFrom);

        return new CustomerAgg(newCustomers, returning, buyersInPeriod.Count);
    }

    private async Task<decimal> SumActiveMrrAsync(CancellationToken cancellationToken) =>
        await commerceDb.MembershipPlans.AsNoTracking()
            .Join(
                commerceDb.MembershipSubscriptions.AsNoTracking()
                    .Where(s => s.Status == MembershipSubscriptionStatus.Active),
                p => p.Id,
                s => s.PlanId,
                (p, _) => p.Price)
            .SumAsync(cancellationToken);

    private async Task<(IReadOnlyList<AnalyticsNamedValueDto> ByCategory, IReadOnlyList<AnalyticsNamedValueDto> ByProduct)>
        AggregateProductCategoryRevenueAsync(
            DateTimeOffset dateFrom,
            DateTimeOffset dateTo,
            CancellationToken cancellationToken)
    {
        var itemRows = await (
            from oi in commerceDb.OrderItems.AsNoTracking()
            join o in commerceDb.Orders.AsNoTracking() on oi.OrderId equals o.Id
            where o.Status == OrderStatus.Completed
                  && o.CreatedOnUtc >= dateFrom
                  && o.CreatedOnUtc < dateTo
                  && oi.ProductId != null
            group oi by new { ProductId = oi.ProductId!.Value, oi.ProductNameEn } into g
            select new
            {
                g.Key.ProductId,
                Name = g.Key.ProductNameEn,
                Qty = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal),
            })
            .ToListAsync(cancellationToken);

        var productIds = itemRows.Select(r => r.ProductId).Distinct().ToList();
        var productMeta = productIds.Count == 0
            ? []
            : await catalogDb.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.NameEn, p.CategoryId })
                .ToListAsync(cancellationToken);

        var categoryIds = productMeta.Select(p => p.CategoryId).Distinct().ToList();
        var categories = categoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await catalogDb.Categories.AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .Select(c => new { c.Id, c.NameEn })
                .ToDictionaryAsync(c => c.Id, c => c.NameEn, cancellationToken);

        var productCategory = productMeta.ToDictionary(p => p.Id, p => p.CategoryId);
        var productNames = productMeta.ToDictionary(p => p.Id, p => p.NameEn);

        var byProduct = itemRows
            .OrderByDescending(r => r.Revenue)
            .Take(50)
            .Select(r => new AnalyticsNamedValueDto(
                productNames.GetValueOrDefault(r.ProductId, r.Name),
                r.Revenue,
                r.Qty,
                r.ProductId))
            .ToList();

        var byCategory = itemRows
            .GroupBy(r => productCategory.TryGetValue(r.ProductId, out var catId) ? catId : Guid.Empty)
            .Select(g =>
            {
                var name = g.Key == Guid.Empty
                    ? "Uncategorized"
                    : categories.GetValueOrDefault(g.Key, "Category");
                return new AnalyticsNamedValueDto(
                    name,
                    g.Sum(x => x.Revenue),
                    g.Sum(x => x.Qty),
                    g.Key == Guid.Empty ? null : g.Key);
            })
            .OrderByDescending(x => x.Value)
            .Take(50)
            .ToList();

        return (byCategory, byProduct);
    }

    private async Task<IReadOnlyList<AnalyticsNamedValueDto>> AggregateCategoryQuantityAsync(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        CancellationToken cancellationToken)
    {
        var (byCategory, _) = await AggregateProductCategoryRevenueAsync(dateFrom, dateTo, cancellationToken);
        return byCategory
            .OrderByDescending(c => c.Count ?? 0)
            .Select(c => new AnalyticsNamedValueDto(c.Name, c.Count ?? 0, c.Count, c.Id))
            .ToList();
    }

    private async Task<IReadOnlyList<AnalyticsNamedValueDto>> AggregateMembershipPlanRevenueAsync(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        CancellationToken cancellationToken)
    {
        var orderPlanRows = await (
            from o in commerceDb.Orders.AsNoTracking()
            where o.Status == OrderStatus.Completed
                  && o.CreatedOnUtc >= dateFrom
                  && o.CreatedOnUtc < dateTo
                  && o.Kind == OrderKind.Membership
            join oi in commerceDb.OrderItems.AsNoTracking() on o.Id equals oi.OrderId
            where oi.MembershipPlanId != null
            select new { o.Id, o.TotalAmount, PlanId = oi.MembershipPlanId!.Value })
            .ToListAsync(cancellationToken);

        var aggregated = orderPlanRows
            .GroupBy(x => x.PlanId)
            .Select(g =>
            {
                var distinctOrders = g.GroupBy(x => x.Id).Select(og => og.First()).ToList();
                return new
                {
                    PlanId = g.Key,
                    Revenue = distinctOrders.Sum(x => x.TotalAmount),
                    Count = distinctOrders.Count,
                };
            })
            .ToList();

        var planIds = aggregated.Select(a => a.PlanId).ToList();
        var planNames = planIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await commerceDb.MembershipPlans.AsNoTracking()
                .Where(p => planIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return aggregated
            .OrderByDescending(a => a.Revenue)
            .Select(a => new AnalyticsNamedValueDto(
                planNames.GetValueOrDefault(a.PlanId, "Plan"),
                a.Revenue,
                a.Count,
                a.PlanId))
            .ToList();
    }

    private async Task<IReadOnlyList<AnalyticsSeriesPointDto>> BuildOrderSeriesAsync(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        Expression<Func<Order, bool>> extraFilter,
        CancellationToken cancellationToken,
        bool countOnly = false)
    {
        var orders = await commerceDb.Orders.AsNoTracking()
            .Where(o => o.CreatedOnUtc >= dateFrom && o.CreatedOnUtc < dateTo)
            .Where(extraFilter)
            .Select(o => new { o.CreatedOnUtc, o.TotalAmount })
            .ToListAsync(cancellationToken);

        return BuildSeriesFromDatedValues(
            dateFrom,
            dateTo,
            orders.Select(o => (o.CreatedOnUtc, countOnly ? 1m : o.TotalAmount)));
    }

    private static IReadOnlyList<AnalyticsSeriesPointDto> BuildSeriesFromDates(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        IEnumerable<DateTimeOffset> dates) =>
        BuildSeriesFromDatedValues(dateFrom, dateTo, dates.Select(d => (d, 1m)));

    private static IReadOnlyList<AnalyticsSeriesPointDto> BuildSeriesFromDateTimes(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        IEnumerable<DateTime> dates) =>
        BuildSeriesFromDatedValues(
            dateFrom,
            dateTo,
            dates.Select(d => (new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)), 1m)));

    private static IReadOnlyList<AnalyticsSeriesPointDto> BuildSeriesFromDatedValues(
        DateTimeOffset dateFrom,
        DateTimeOffset dateTo,
        IEnumerable<(DateTimeOffset At, decimal Value)> points)
    {
        var useDay = (dateTo - dateFrom).TotalDays <= 90;
        var list = points.ToList();

        if (useDay)
        {
            var buckets = new SortedDictionary<DateTime, (decimal Value, int Count)>();
            for (var day = dateFrom.UtcDateTime.Date; day < dateTo.UtcDateTime; day = day.AddDays(1))
            {
                buckets[day] = (0m, 0);
            }

            foreach (var (at, value) in list)
            {
                var key = at.UtcDateTime.Date;
                if (!buckets.ContainsKey(key))
                {
                    continue;
                }

                var current = buckets[key];
                buckets[key] = (current.Value + value, current.Count + 1);
            }

            return buckets
                .Select(kv => new AnalyticsSeriesPointDto(
                    kv.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    kv.Value.Value,
                    kv.Value.Count))
                .ToList();
        }

        var monthBuckets = new SortedDictionary<DateTime, (decimal Value, int Count)>();
        var cursor = new DateTime(dateFrom.UtcDateTime.Year, dateFrom.UtcDateTime.Month, 1);
        var endMonth = new DateTime(dateTo.UtcDateTime.Year, dateTo.UtcDateTime.Month, 1);
        for (; cursor <= endMonth; cursor = cursor.AddMonths(1))
        {
            monthBuckets[cursor] = (0m, 0);
        }

        foreach (var (at, value) in list)
        {
            var key = new DateTime(at.UtcDateTime.Year, at.UtcDateTime.Month, 1);
            if (!monthBuckets.ContainsKey(key))
            {
                continue;
            }

            var current = monthBuckets[key];
            monthBuckets[key] = (current.Value + value, current.Count + 1);
        }

        return monthBuckets
            .Select(kv => new AnalyticsSeriesPointDto(
                kv.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                kv.Value.Value,
                kv.Value.Count))
            .ToList();
    }

    private static AnalyticsExportDto BuildJsonExport(string section, object dto)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto, dto.GetType(), JsonOptions));
        return new AnalyticsExportDto(
            $"analytics-{section}-{DateTime.UtcNow:yyyyMMddHHmmss}.json",
            "application/json",
            bytes);
    }

    private static AnalyticsExportDto BuildCsvExport(string section, object dto, bool excel = false)
    {
        var rows = FlattenDto(dto);
        var sb = new StringBuilder();
        sb.AppendLine("Metric,Value");
        foreach (var (key, value) in rows)
        {
            sb.Append(EscapeCsv(key));
            sb.Append(',');
            sb.AppendLine(EscapeCsv(value));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var ext = excel ? "xls" : "csv";
        var contentType = excel ? "application/vnd.ms-excel" : "text/csv";
        return new AnalyticsExportDto(
            $"analytics-{section}-{DateTime.UtcNow:yyyyMMddHHmmss}.{ext}",
            contentType,
            bytes);
    }

    private static AnalyticsExportDto BuildHtmlExport(string section, object dto)
    {
        var rows = FlattenDto(dto);
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>Analytics {section}</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:2rem}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ccc;padding:.5rem;text-align:left}th{background:#f5f5f5}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h1>Analytics — {System.Net.WebUtility.HtmlEncode(section)}</h1>");
        sb.AppendLine("<table><thead><tr><th>Metric</th><th>Value</th></tr></thead><tbody>");
        foreach (var (key, value) in rows)
        {
            sb.Append("<tr><td>");
            sb.Append(System.Net.WebUtility.HtmlEncode(key));
            sb.Append("</td><td>");
            sb.Append(System.Net.WebUtility.HtmlEncode(value));
            sb.AppendLine("</td></tr>");
        }

        sb.AppendLine("</tbody></table></body></html>");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return new AnalyticsExportDto(
            $"analytics-{section}-{DateTime.UtcNow:yyyyMMddHHmmss}.html",
            "text/html",
            bytes);
    }

    private static List<(string Key, string Value)> FlattenDto(object dto)
    {
        var rows = new List<(string, string)>();
        foreach (var prop in dto.GetType().GetProperties())
        {
            var value = prop.GetValue(dto);
            if (value is null)
            {
                rows.Add((prop.Name, string.Empty));
                continue;
            }

            if (value is AnalyticsPeriodDto period)
            {
                rows.Add(("Period.DateFrom", period.DateFrom.ToString("o", CultureInfo.InvariantCulture)));
                rows.Add(("Period.DateTo", period.DateTo.ToString("o", CultureInfo.InvariantCulture)));
                rows.Add(("Period.Preset", period.Preset));
                rows.Add(("Period.ComparisonMode", period.ComparisonMode));
                continue;
            }

            if (value is AnalyticsGrowthDto growth)
            {
                rows.Add(($"{prop.Name}.Current", growth.Current.ToString(CultureInfo.InvariantCulture)));
                rows.Add(($"{prop.Name}.Previous", growth.Previous.ToString(CultureInfo.InvariantCulture)));
                rows.Add(($"{prop.Name}.PercentChange", growth.PercentChange?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
                continue;
            }

            if (value is System.Collections.IEnumerable enumerable and not string)
            {
                var i = 0;
                foreach (var item in enumerable)
                {
                    rows.Add(($"{prop.Name}[{i}]", item?.ToString() ?? string.Empty));
                    i++;
                    if (i >= 50)
                    {
                        break;
                    }
                }

                continue;
            }

            rows.Add((prop.Name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return rows;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private readonly record struct RevenueAgg(
        decimal Gross,
        decimal Net,
        decimal Pending,
        decimal Refunded,
        decimal Aov);

    private readonly record struct CustomerAgg(
        int NewCustomers,
        int ReturningCustomers,
        int TotalDistinctBuyers);
}
