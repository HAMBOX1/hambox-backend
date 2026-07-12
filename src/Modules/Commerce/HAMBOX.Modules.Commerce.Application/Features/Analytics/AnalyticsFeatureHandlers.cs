using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Analytics;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Application.Abstractions;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Analytics;

public sealed record GetAnalyticsOverviewQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsOverviewDto>>;

public sealed record GetAnalyticsRevenueQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsRevenueDto>>;

public sealed record GetAnalyticsOrdersQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsOrdersDto>>;

public sealed record GetAnalyticsProductsQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsProductsDto>>;

public sealed record GetAnalyticsCategoriesQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsCategoriesDto>>;

public sealed record GetAnalyticsCustomersQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsCustomersDto>>;

public sealed record GetAnalyticsMembershipsQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsMembershipsDto>>;

public sealed record GetAnalyticsPromotionsQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsPromotionsDto>>;

public sealed record GetAnalyticsReferralsQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsReferralsDto>>;

public sealed record GetAnalyticsSearchQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsSearchDto>>;

public sealed record GetAnalyticsOperationsQuery(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsOperationsDto>>;

public sealed record ExportAnalyticsQuery(
    string Section,
    string Format,
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Compare) : IRequest<Result<AnalyticsExportDto>>;

public static class AnalyticsPeriodResolver
{
    public static Result<AnalyticsPeriodRequest> Resolve(
        string? preset,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? compare)
    {
        var comparison = ParseComparison(compare);
        var now = DateTimeOffset.UtcNow;
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);

        var presetKey = (preset ?? "Last30").Trim();
        DateTimeOffset dateFrom;
        DateTimeOffset dateTo;

        switch (presetKey.ToLowerInvariant())
        {
            case "today":
                dateFrom = today;
                dateTo = today.AddDays(1);
                break;
            case "yesterday":
                dateFrom = today.AddDays(-1);
                dateTo = today;
                break;
            case "last7":
                dateFrom = today.AddDays(-6);
                dateTo = today.AddDays(1);
                break;
            case "last30":
                dateFrom = today.AddDays(-29);
                dateTo = today.AddDays(1);
                break;
            case "last90":
                dateFrom = today.AddDays(-89);
                dateTo = today.AddDays(1);
                break;
            case "thismonth":
                dateFrom = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                dateTo = today.AddDays(1);
                break;
            case "thisyear":
                dateFrom = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
                dateTo = today.AddDays(1);
                break;
            case "custom":
                if (!from.HasValue || !to.HasValue)
                {
                    return Result.Failure<AnalyticsPeriodRequest>(
                        new Error("Analytics.CustomRangeRequired", "Custom preset requires from and to query parameters."));
                }

                dateFrom = from.Value;
                dateTo = to.Value;
                break;
            default:
                dateFrom = today.AddDays(-29);
                dateTo = today.AddDays(1);
                break;
        }

        if (dateTo <= dateFrom)
        {
            return Result.Failure<AnalyticsPeriodRequest>(
                new Error("Analytics.InvalidRange", "Date range end must be after start."));
        }

        return Result.Success(new AnalyticsPeriodRequest(dateFrom, dateTo, comparison));
    }

    private static AnalyticsComparisonMode ParseComparison(string? compare) =>
        (compare ?? "None").Trim().ToLowerInvariant() switch
        {
            "previousperiod" or "previous" => AnalyticsComparisonMode.PreviousPeriod,
            "previousmonth" => AnalyticsComparisonMode.PreviousMonth,
            "previousyear" => AnalyticsComparisonMode.PreviousYear,
            _ => AnalyticsComparisonMode.None,
        };
}

internal sealed class GetAnalyticsOverviewQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsOverviewQuery, Result<AnalyticsOverviewDto>>
{
    public async Task<Result<AnalyticsOverviewDto>> Handle(GetAnalyticsOverviewQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsOverviewDto>(period.Error);
        }

        return Result.Success(await analytics.GetOverviewAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsRevenueQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsRevenueQuery, Result<AnalyticsRevenueDto>>
{
    public async Task<Result<AnalyticsRevenueDto>> Handle(GetAnalyticsRevenueQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsRevenueDto>(period.Error);
        }

        return Result.Success(await analytics.GetRevenueAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsOrdersQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsOrdersQuery, Result<AnalyticsOrdersDto>>
{
    public async Task<Result<AnalyticsOrdersDto>> Handle(GetAnalyticsOrdersQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsOrdersDto>(period.Error);
        }

        return Result.Success(await analytics.GetOrdersAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsProductsQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsProductsQuery, Result<AnalyticsProductsDto>>
{
    public async Task<Result<AnalyticsProductsDto>> Handle(GetAnalyticsProductsQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsProductsDto>(period.Error);
        }

        return Result.Success(await analytics.GetProductsAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsCategoriesQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsCategoriesQuery, Result<AnalyticsCategoriesDto>>
{
    public async Task<Result<AnalyticsCategoriesDto>> Handle(GetAnalyticsCategoriesQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsCategoriesDto>(period.Error);
        }

        return Result.Success(await analytics.GetCategoriesAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsCustomersQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsCustomersQuery, Result<AnalyticsCustomersDto>>
{
    public async Task<Result<AnalyticsCustomersDto>> Handle(GetAnalyticsCustomersQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsCustomersDto>(period.Error);
        }

        return Result.Success(await analytics.GetCustomersAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsMembershipsQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsMembershipsQuery, Result<AnalyticsMembershipsDto>>
{
    public async Task<Result<AnalyticsMembershipsDto>> Handle(GetAnalyticsMembershipsQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsMembershipsDto>(period.Error);
        }

        return Result.Success(await analytics.GetMembershipsAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsPromotionsQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsPromotionsQuery, Result<AnalyticsPromotionsDto>>
{
    public async Task<Result<AnalyticsPromotionsDto>> Handle(GetAnalyticsPromotionsQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsPromotionsDto>(period.Error);
        }

        return Result.Success(await analytics.GetPromotionsAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsReferralsQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsReferralsQuery, Result<AnalyticsReferralsDto>>
{
    public async Task<Result<AnalyticsReferralsDto>> Handle(GetAnalyticsReferralsQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsReferralsDto>(period.Error);
        }

        return Result.Success(await analytics.GetReferralsAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsSearchQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsSearchQuery, Result<AnalyticsSearchDto>>
{
    public async Task<Result<AnalyticsSearchDto>> Handle(GetAnalyticsSearchQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsSearchDto>(period.Error);
        }

        return Result.Success(await analytics.GetSearchAsync(period.Value, cancellationToken));
    }
}

internal sealed class GetAnalyticsOperationsQueryHandler(IAnalyticsAggregationService analytics)
    : IRequestHandler<GetAnalyticsOperationsQuery, Result<AnalyticsOperationsDto>>
{
    public async Task<Result<AnalyticsOperationsDto>> Handle(GetAnalyticsOperationsQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsOperationsDto>(period.Error);
        }

        return Result.Success(await analytics.GetOperationsAsync(period.Value, cancellationToken));
    }
}

internal sealed class ExportAnalyticsQueryHandler(
    IAnalyticsAggregationService analytics,
    ICommerceDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<ExportAnalyticsQuery, Result<AnalyticsExportDto>>
{
    public async Task<Result<AnalyticsExportDto>> Handle(ExportAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var period = AnalyticsPeriodResolver.Resolve(request.Preset, request.From, request.To, request.Compare);
        if (!period.IsSuccess)
        {
            return Result.Failure<AnalyticsExportDto>(period.Error);
        }

        var export = await analytics.ExportAsync(
            request.Section,
            request.Format,
            period.Value,
            cancellationToken);

        db.OperationalAuditLogs.Add(OperationalAuditLog.Create(
            "AnalyticsExported",
            currentUser.UserId,
            currentUser.UserId,
            "Analytics",
            request.Section,
            $"format={request.Format};from={period.Value.DateFrom:o};to={period.Value.DateTo:o}"));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(export);
    }
}
