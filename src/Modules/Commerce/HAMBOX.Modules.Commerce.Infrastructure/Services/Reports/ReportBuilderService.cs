using System.Globalization;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Analytics;
using HAMBOX.Modules.Commerce.Application.Contracts.Reports;
using HAMBOX.Modules.Commerce.Application.Features.Analytics;
using HAMBOX.Modules.Commerce.Domain.Reports;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services.Reports;

internal sealed class ReportBuilderService(
    IAnalyticsAggregationService analytics,
    ICommerceDbContext commerceDb,
    IInventoryEngine inventoryEngine,
    IPlatformSettingsProvider platformSettings,
    IReportCatalog catalog) : IReportBuilderService
{
    public async Task<ReportModel> BuildAsync(
        string reportType,
        ReportFilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        var typeInfo = catalog.GetType(reportType)
            ?? throw new InvalidOperationException($"Unknown report type '{reportType}'.");

        var periodResult = ResolvePeriod(filters);
        if (!periodResult.IsSuccess)
        {
            throw new InvalidOperationException(periodResult.Error.Description);
        }

        var period = periodResult.Value;
        var general = await platformSettings.GetGeneralAsync(cancellationToken);
        var brandName = string.IsNullOrWhiteSpace(general.StoreName) ? "HAMBOX" : general.StoreName;
        var filtersSummary = BuildFiltersSummary(filters, period);

        return reportType.Trim() switch
        {
            var t when Equals(t, ReportTypes.Sales) || Equals(t, ReportTypes.Revenue) =>
                await BuildRevenueAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            var t when Equals(t, ReportTypes.Orders) =>
                await BuildOrdersAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            var t when Equals(t, ReportTypes.Inventory) =>
                await BuildInventoryAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            var t when Equals(t, ReportTypes.Products) =>
                await BuildProductsAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            var t when Equals(t, ReportTypes.Categories) =>
                await BuildCategoriesAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            var t when Equals(t, ReportTypes.Membership) =>
                await BuildMembershipsAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            var t when Equals(t, ReportTypes.Promotion) || Equals(t, ReportTypes.Coupon) =>
                await BuildPromotionsAsync(typeInfo.Name, brandName, filtersSummary, period, Equals(t, ReportTypes.Coupon), cancellationToken),
            var t when Equals(t, ReportTypes.Referral) =>
                await BuildReferralsAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            var t when Equals(t, ReportTypes.Customer) =>
                await BuildCustomersAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            var t when Equals(t, ReportTypes.Operations) =>
                await BuildOperationsAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            var t when Equals(t, ReportTypes.Audit) =>
                await BuildAuditAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
            _ => await BuildOverviewSalesAsync(typeInfo.Name, brandName, filtersSummary, period, cancellationToken),
        };
    }

    private static bool Equals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static Result<AnalyticsPeriodRequest> ResolvePeriod(ReportFilterRequest filters) =>
        AnalyticsPeriodResolver.Resolve(filters.Preset, filters.From, filters.To, null);

    private static IReadOnlyDictionary<string, string> BuildFiltersSummary(
        ReportFilterRequest filters,
        AnalyticsPeriodRequest period)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["From"] = period.DateFrom.ToString("u", CultureInfo.InvariantCulture),
            ["To"] = period.DateTo.ToString("u", CultureInfo.InvariantCulture),
            ["Preset"] = filters.Preset ?? "Custom",
        };

        if (!string.IsNullOrWhiteSpace(filters.Status)) map["Status"] = filters.Status!;
        if (filters.CategoryId.HasValue) map["CategoryId"] = filters.CategoryId.Value.ToString();
        if (filters.MembershipPlanId.HasValue) map["MembershipPlanId"] = filters.MembershipPlanId.Value.ToString();
        if (filters.PromotionId.HasValue) map["PromotionId"] = filters.PromotionId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(filters.Country)) map["Country"] = filters.Country!;
        if (!string.IsNullOrWhiteSpace(filters.Currency)) map["Currency"] = filters.Currency!;
        return map;
    }

    private async Task<ReportModel> BuildOverviewSalesAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var dto = await analytics.GetOverviewAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                "Key metrics",
                Kpis:
                [
                    Kpi("Gross revenue", dto.GrossRevenue),
                    Kpi("Net revenue", dto.NetRevenue),
                    Kpi("Orders", dto.TotalOrders),
                    Kpi("Completed orders", dto.CompletedOrders),
                    Kpi("AOV", dto.AverageOrderValue),
                    Kpi("New customers", dto.NewCustomers),
                ],
                Chart: ToChart("Revenue series", dto.RevenueSeries)),
            NamedTable("Orders series", dto.OrdersSeries),
        };

        return Model(ReportTypes.Sales, title, brand, filters, sections,
        [
            Kpi("Gross revenue", dto.GrossRevenue),
            Kpi("Net revenue", dto.NetRevenue),
            Kpi("Total orders", dto.TotalOrders),
        ]);
    }

    private async Task<ReportModel> BuildRevenueAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var dto = await analytics.GetRevenueAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                "Revenue totals",
                Kpis:
                [
                    Kpi("Gross", dto.GrossRevenue),
                    Kpi("Net", dto.NetRevenue),
                    Kpi("Pending", dto.PendingRevenue),
                    Kpi("Refunded", dto.RefundedRevenue),
                    Kpi("AOV", dto.AverageOrderValue),
                    Kpi("Product revenue", dto.ProductRevenue),
                    Kpi("Membership revenue", dto.MembershipRevenue),
                ],
                Chart: ToChart("Revenue series", dto.Series)),
            NamedValueTable("By category", dto.ByCategory),
            NamedValueTable("By product", dto.ByProduct),
            NamedValueTable("By membership plan", dto.ByMembershipPlan),
        };

        return Model(ReportTypes.Revenue, title, brand, filters, sections,
        [
            Kpi("Gross", dto.GrossRevenue),
            Kpi("Net", dto.NetRevenue),
        ]);
    }

    private async Task<ReportModel> BuildOrdersAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var dto = await analytics.GetOrdersAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                "Order volumes",
                Kpis:
                [
                    Kpi("Total", dto.Total),
                    Kpi("Pending", dto.Pending),
                    Kpi("Processing", dto.Processing),
                    Kpi("Completed", dto.Completed),
                    Kpi("Cancelled", dto.Cancelled),
                    Kpi("Refunded", dto.Refunded),
                    Kpi("Failed", dto.Failed),
                    Kpi("Conversion %", dto.ConversionRate),
                ],
                Chart: ToChart("Orders series", dto.Series)),
            NamedValueTable("By status", dto.ByStatus),
        };

        return Model(ReportTypes.Orders, title, brand, filters, sections, [Kpi("Total orders", dto.Total)]);
    }

    private async Task<ReportModel> BuildInventoryAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var stats = await inventoryEngine.GetStatisticsAsync(cancellationToken: ct);
        var products = await analytics.GetProductsAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                "Inventory snapshot",
                Kpis:
                [
                    Kpi("Available", stats.Available),
                    Kpi("Reserved", stats.Reserved),
                    Kpi("Sold", stats.Sold),
                    Kpi("Expired", stats.Expired),
                    Kpi("Low stock variants", stats.LowStockVariants),
                    Kpi("Out of stock variants", stats.OutOfStockVariants),
                    Kpi("Inventory value", stats.InventoryValue),
                    Kpi("Estimated profit", stats.EstimatedProfit),
                ]),
            NamedValueTable("Top by quantity", products.TopByQuantity),
            NamedValueTable("Worst by quantity", products.WorstByQuantity),
        };

        return Model(ReportTypes.Inventory, title, brand, filters, sections,
        [
            Kpi("Available", stats.Available),
            Kpi("Inventory value", stats.InventoryValue),
        ]);
    }

    private async Task<ReportModel> BuildProductsAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var dto = await analytics.GetProductsAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                "Product inventory signals",
                Kpis:
                [
                    Kpi("Out of stock", dto.OutOfStockVariants),
                    Kpi("Low stock", dto.LowStockVariants),
                    Kpi("Inventory value", dto.InventoryValue),
                    Kpi("Turnover ratio", dto.TurnoverRatio),
                ]),
            NamedValueTable("Top by quantity", dto.TopByQuantity),
            NamedValueTable("Top by revenue", dto.TopByRevenue),
            NamedValueTable("Most viewed", dto.MostViewed),
            NamedValueTable("Never purchased", dto.NeverPurchased),
        };

        return Model(ReportTypes.Products, title, brand, filters, sections,
        [
            Kpi("Inventory value", dto.InventoryValue),
            Kpi("Low stock", dto.LowStockVariants),
        ]);
    }

    private async Task<ReportModel> BuildCategoriesAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var dto = await analytics.GetCategoriesAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            NamedValueTable("By revenue", dto.ByRevenue),
            NamedValueTable("By quantity", dto.ByQuantity),
            NamedTable("Series", dto.Series),
        };

        return Model(ReportTypes.Categories, title, brand, filters, sections);
    }

    private async Task<ReportModel> BuildMembershipsAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var dto = await analytics.GetMembershipsAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                "Membership metrics",
                Kpis:
                [
                    Kpi("Active", dto.ActiveCount),
                    Kpi("New", dto.NewInPeriod),
                    Kpi("Renewals", dto.RenewalsInPeriod),
                    Kpi("Cancellations", dto.CancellationsInPeriod),
                    Kpi("MRR", dto.Mrr),
                    Kpi("ARR", dto.Arr),
                    Kpi("Revenue", dto.MembershipRevenue),
                ]),
            NamedValueTable("By plan", dto.RevenueByPlan),
            NamedTable("Series", dto.Series),
        };

        return Model(ReportTypes.Membership, title, brand, filters, sections,
        [
            Kpi("Active", dto.ActiveCount),
            Kpi("MRR", dto.Mrr),
        ]);
    }

    private async Task<ReportModel> BuildPromotionsAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        bool couponFocus,
        CancellationToken ct)
    {
        var dto = await analytics.GetPromotionsAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                couponFocus ? "Coupon metrics" : "Promotion metrics",
                Kpis:
                [
                    Kpi("Redemptions", dto.Redemptions),
                    Kpi("Discount total", dto.DiscountTotal),
                    Kpi("Revenue on promo orders", dto.RevenueOnPromoOrders),
                    Kpi("Conversion %", dto.ConversionRate),
                ]),
            NamedValueTable("Top promotions", dto.TopPromotions),
            NamedValueTable("Top coupons", dto.TopCoupons),
            NamedTable("Series", dto.Series),
        };

        return Model(
            couponFocus ? ReportTypes.Coupon : ReportTypes.Promotion,
            title,
            brand,
            filters,
            sections,
            [Kpi("Redemptions", dto.Redemptions), Kpi("Discount total", dto.DiscountTotal)]);
    }

    private async Task<ReportModel> BuildReferralsAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var dto = await analytics.GetReferralsAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                "Referral metrics",
                Kpis:
                [
                    Kpi("Invites", dto.Invites),
                    Kpi("Successful", dto.Successful),
                    Kpi("Conversion %", dto.ConversionRate),
                    Kpi("Attributed revenue", dto.AttributedRevenue),
                ]),
            NamedValueTable("Top referrers", dto.TopReferrers),
            NamedTable("Series", dto.Series),
        };

        return Model(ReportTypes.Referral, title, brand, filters, sections,
        [
            Kpi("Invites", dto.Invites),
            Kpi("Successful", dto.Successful),
        ]);
    }

    private async Task<ReportModel> BuildCustomersAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var dto = await analytics.GetCustomersAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                "Customer metrics",
                Kpis:
                [
                    Kpi("New", dto.NewCustomers),
                    Kpi("Returning", dto.ReturningCustomers),
                    Kpi("Distinct buyers", dto.TotalDistinctBuyers),
                    Kpi("Avg lifetime value", dto.AverageLifetimeValue),
                ],
                Chart: ToChart("New customers series", dto.NewCustomersSeries)),
            NamedValueTable("By country", dto.ByCountry),
            NamedValueTable("Top by lifetime value", dto.TopByLifetimeValue),
        };

        return Model(ReportTypes.Customer, title, brand, filters, sections,
        [
            Kpi("New", dto.NewCustomers),
            Kpi("Returning", dto.ReturningCustomers),
        ]);
    }

    private async Task<ReportModel> BuildOperationsAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var dto = await analytics.GetOperationsAsync(period, ct);
        var sections = new List<ReportSectionDto>
        {
            new(
                "Operations health",
                Kpis:
                [
                    Kpi("Failed jobs", dto.FailedJobs),
                    Kpi("API 5xx", dto.Api5xxCount),
                    Kpi("Inactive suppliers", dto.InactiveSuppliers),
                    Kpi("Retry success %", dto.RetrySuccessRate),
                    Kpi("Avg queue (s)", (decimal)(dto.AverageQueueSeconds ?? 0)),
                    Kpi("Worker uptime %", (decimal)(dto.WorkerUptimePercent ?? 0)),
                ],
                Chart: ToChart("Failed jobs series", dto.FailedJobsSeries)),
            NamedTable("API 5xx series", dto.Api5xxSeries),
        };

        return Model(ReportTypes.Operations, title, brand, filters, sections,
        [
            Kpi("Failed jobs", dto.FailedJobs),
            Kpi("API 5xx", dto.Api5xxCount),
        ]);
    }

    private async Task<ReportModel> BuildAuditAsync(
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        AnalyticsPeriodRequest period,
        CancellationToken ct)
    {
        var logs = await commerceDb.OperationalAuditLogs.AsNoTracking()
            .Where(x => x.OccurredOnUtc >= period.DateFrom && x.OccurredOnUtc < period.DateTo)
            .OrderByDescending(x => x.OccurredOnUtc)
            .Take(500)
            .Select(x => new
            {
                x.OccurredOnUtc,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.ActorUserId,
                x.Details,
            })
            .ToListAsync(ct);

        var table = new ReportTableDto(
            "Audit entries",
            [
                new("when", "When (UTC)"),
                new("action", "Action"),
                new("entity", "Entity"),
                new("actor", "Actor"),
                new("details", "Details"),
            ],
            logs.Select(x => new ReportTableRowDto(new Dictionary<string, string>
            {
                ["when"] = x.OccurredOnUtc.ToString("u", CultureInfo.InvariantCulture),
                ["action"] = x.Action,
                ["entity"] = $"{x.EntityType}:{x.EntityId}",
                ["actor"] = x.ActorUserId ?? string.Empty,
                ["details"] = x.Details ?? string.Empty,
            })).ToList());

        var sections = new List<ReportSectionDto>
        {
            new("Audit log", Kpis: [Kpi("Entries", logs.Count)], Table: table),
        };

        return Model(ReportTypes.Audit, title, brand, filters, sections, [Kpi("Entries", logs.Count)]);
    }

    private static ReportModel Model(
        string type,
        string title,
        string brand,
        IReadOnlyDictionary<string, string> filters,
        IReadOnlyList<ReportSectionDto> sections,
        IReadOnlyList<ReportKpiDto>? totals = null) =>
        new(type, title, brand, DateTimeOffset.UtcNow, filters, sections, totals);

    private static ReportKpiDto Kpi(string label, decimal value) =>
        new(label, value.ToString("0.##", CultureInfo.InvariantCulture));

    private static ReportKpiDto Kpi(string label, int value) =>
        new(label, value.ToString(CultureInfo.InvariantCulture));

    private static ReportChartSeriesDto? ToChart(string title, IReadOnlyList<AnalyticsSeriesPointDto> series)
    {
        if (series.Count == 0)
        {
            return null;
        }

        return new ReportChartSeriesDto(
            title,
            series.Select(s => s.Label).ToList(),
            series.Select(s => s.Value).ToList());
    }

    private static ReportSectionDto NamedTable(string title, IReadOnlyList<AnalyticsSeriesPointDto> series) =>
        new(
            title,
            Table: new ReportTableDto(
                title,
                [new("label", "Label"), new("value", "Value"), new("count", "Count")],
                series.Select(s => new ReportTableRowDto(new Dictionary<string, string>
                {
                    ["label"] = s.Label,
                    ["value"] = s.Value.ToString("0.##", CultureInfo.InvariantCulture),
                    ["count"] = s.Count?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                })).ToList()));

    private static ReportSectionDto NamedValueTable(string title, IReadOnlyList<AnalyticsNamedValueDto> values) =>
        new(
            title,
            Table: new ReportTableDto(
                title,
                [new("name", "Name"), new("value", "Value"), new("count", "Count")],
                values.Select(s => new ReportTableRowDto(new Dictionary<string, string>
                {
                    ["name"] = s.Name,
                    ["value"] = s.Value.ToString("0.##", CultureInfo.InvariantCulture),
                    ["count"] = s.Count?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                })).ToList()));
}
