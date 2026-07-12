using HAMBOX.Modules.Commerce.Application.Contracts.Analytics;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

public interface IAnalyticsAggregationService
{
    Task<AnalyticsOverviewDto> GetOverviewAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsRevenueDto> GetRevenueAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsOrdersDto> GetOrdersAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsProductsDto> GetProductsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsCategoriesDto> GetCategoriesAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsCustomersDto> GetCustomersAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsMembershipsDto> GetMembershipsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsPromotionsDto> GetPromotionsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsReferralsDto> GetReferralsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsSearchDto> GetSearchAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsOperationsDto> GetOperationsAsync(
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyticsExportDto> ExportAsync(
        string section,
        string format,
        AnalyticsPeriodRequest request,
        CancellationToken cancellationToken = default);
}
