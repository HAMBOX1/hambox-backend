namespace HAMBOX.Modules.Commerce.Application.Contracts.Analytics;

public enum AnalyticsComparisonMode
{
    None = 0,
    PreviousPeriod = 1,
    PreviousMonth = 2,
    PreviousYear = 3,
}

public enum AnalyticsPeriodPreset
{
    Today = 0,
    Yesterday = 1,
    Last7 = 2,
    Last30 = 3,
    Last90 = 4,
    ThisMonth = 5,
    ThisYear = 6,
    Custom = 7,
}

public sealed record AnalyticsPeriodRequest(
    DateTimeOffset DateFrom,
    DateTimeOffset DateTo,
    AnalyticsComparisonMode ComparisonMode);

public sealed record AnalyticsPeriodDto(
    DateTimeOffset DateFrom,
    DateTimeOffset DateTo,
    string Preset,
    string ComparisonMode,
    DateTimeOffset? ComparisonFrom,
    DateTimeOffset? ComparisonTo);

public sealed record AnalyticsSeriesPointDto(string Label, decimal Value, int? Count = null);

public sealed record AnalyticsNamedValueDto(string Name, decimal Value, int? Count = null, Guid? Id = null);

public sealed record AnalyticsGrowthDto(decimal Current, decimal Previous, decimal? PercentChange);

public sealed record AnalyticsOverviewDto(
    AnalyticsPeriodDto Period,
    decimal GrossRevenue,
    decimal NetRevenue,
    decimal PendingRevenue,
    decimal RefundedRevenue,
    decimal AverageOrderValue,
    int TotalOrders,
    int CompletedOrders,
    decimal OrderConversionRate,
    int NewCustomers,
    int ReturningCustomers,
    int ActiveMemberships,
    decimal MembershipMrr,
    int PromotionRedemptions,
    decimal PromotionDiscountTotal,
    int ReferralInvites,
    int SuccessfulReferrals,
    int SearchQueries,
    int ZeroResultSearches,
    int ProductViews,
    int FailedJobs,
    int Api5xxCount,
    AnalyticsGrowthDto RevenueGrowth,
    AnalyticsGrowthDto OrdersGrowth,
    IReadOnlyList<AnalyticsSeriesPointDto> RevenueSeries,
    IReadOnlyList<AnalyticsSeriesPointDto> OrdersSeries);

public sealed record AnalyticsRevenueDto(
    AnalyticsPeriodDto Period,
    decimal GrossRevenue,
    decimal NetRevenue,
    decimal PendingRevenue,
    decimal RefundedRevenue,
    decimal AverageOrderValue,
    AnalyticsGrowthDto Growth,
    decimal MembershipRevenue,
    decimal ProductRevenue,
    IReadOnlyList<AnalyticsSeriesPointDto> Series,
    IReadOnlyList<AnalyticsNamedValueDto> ByCategory,
    IReadOnlyList<AnalyticsNamedValueDto> ByProduct,
    IReadOnlyList<AnalyticsNamedValueDto> ByMembershipPlan);

public sealed record AnalyticsOrdersDto(
    AnalyticsPeriodDto Period,
    int Total,
    int Pending,
    int Processing,
    int Completed,
    int Cancelled,
    int Refunded,
    int Failed,
    decimal ConversionRate,
    double? AverageProcessingSeconds,
    double? AverageFulfillmentSeconds,
    AnalyticsGrowthDto Growth,
    IReadOnlyList<AnalyticsSeriesPointDto> Series,
    IReadOnlyList<AnalyticsNamedValueDto> ByStatus);

public sealed record AnalyticsProductsDto(
    AnalyticsPeriodDto Period,
    int OutOfStockVariants,
    int LowStockVariants,
    decimal InventoryValue,
    decimal TurnoverRatio,
    IReadOnlyList<AnalyticsNamedValueDto> TopByQuantity,
    IReadOnlyList<AnalyticsNamedValueDto> TopByRevenue,
    IReadOnlyList<AnalyticsNamedValueDto> WorstByQuantity,
    IReadOnlyList<AnalyticsNamedValueDto> MostViewed,
    IReadOnlyList<AnalyticsNamedValueDto> NeverPurchased);

public sealed record AnalyticsCategoriesDto(
    AnalyticsPeriodDto Period,
    IReadOnlyList<AnalyticsNamedValueDto> ByRevenue,
    IReadOnlyList<AnalyticsNamedValueDto> ByQuantity,
    IReadOnlyList<AnalyticsSeriesPointDto> Series);

public sealed record AnalyticsCustomersDto(
    AnalyticsPeriodDto Period,
    int NewCustomers,
    int ReturningCustomers,
    int TotalDistinctBuyers,
    decimal AverageLifetimeValue,
    AnalyticsGrowthDto Growth,
    IReadOnlyList<AnalyticsNamedValueDto> TopByLifetimeValue,
    IReadOnlyList<AnalyticsNamedValueDto> ByCountry,
    IReadOnlyList<AnalyticsSeriesPointDto> NewCustomersSeries);

public sealed record AnalyticsMembershipsDto(
    AnalyticsPeriodDto Period,
    int ActiveCount,
    int NewInPeriod,
    int RenewalsInPeriod,
    int CancellationsInPeriod,
    decimal Mrr,
    decimal Arr,
    decimal ConversionRate,
    decimal MembershipRevenue,
    IReadOnlyList<AnalyticsNamedValueDto> RevenueByPlan,
    IReadOnlyList<AnalyticsSeriesPointDto> Series);

public sealed record AnalyticsPromotionsDto(
    AnalyticsPeriodDto Period,
    int Redemptions,
    decimal DiscountTotal,
    decimal RevenueOnPromoOrders,
    decimal ConversionRate,
    decimal? Roi,
    IReadOnlyList<AnalyticsNamedValueDto> TopCoupons,
    IReadOnlyList<AnalyticsNamedValueDto> TopPromotions,
    IReadOnlyList<AnalyticsSeriesPointDto> Series);

public sealed record AnalyticsReferralsDto(
    AnalyticsPeriodDto Period,
    int Invites,
    int Successful,
    decimal ConversionRate,
    decimal AttributedRevenue,
    AnalyticsGrowthDto Growth,
    IReadOnlyList<AnalyticsNamedValueDto> TopReferrers,
    IReadOnlyList<AnalyticsSeriesPointDto> Series);

public sealed record AnalyticsSearchDto(
    AnalyticsPeriodDto Period,
    int TotalQueries,
    int ZeroResultQueries,
    decimal ZeroResultRate,
    decimal ConversionRate,
    IReadOnlyList<AnalyticsNamedValueDto> TopTerms,
    IReadOnlyList<AnalyticsNamedValueDto> ZeroResultTerms,
    IReadOnlyList<AnalyticsSeriesPointDto> Series);

public sealed record AnalyticsOperationsDto(
    AnalyticsPeriodDto Period,
    double? AverageQueueSeconds,
    double? AverageDeliverySeconds,
    int FailedJobs,
    decimal RetrySuccessRate,
    int InactiveSuppliers,
    int Api5xxCount,
    double? WorkerUptimePercent,
    IReadOnlyList<AnalyticsSeriesPointDto> FailedJobsSeries,
    IReadOnlyList<AnalyticsSeriesPointDto> Api5xxSeries);

public sealed record AnalyticsExportDto(
    string FileName,
    string ContentType,
    byte[] Content);
