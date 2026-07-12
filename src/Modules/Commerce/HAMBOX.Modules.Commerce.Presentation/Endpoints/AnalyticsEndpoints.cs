using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Analytics;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/analytics")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Analytics")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("overview", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsOverviewQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsOverview")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("revenue", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsRevenueQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsRevenue")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("orders", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsOrdersQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsOrders")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("products", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsProductsQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsProducts")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("categories", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsCategoriesQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsCategories")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("customers", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsCustomersQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsCustomers")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("memberships", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsMembershipsQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsMemberships")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("promotions", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsPromotionsQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsPromotions")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("referrals", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsReferralsQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsReferrals")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("search", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsSearchQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsSearch")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("operations", async Task<IResult> (
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
            MapResult(await sender.Send(new GetAnalyticsOperationsQuery(preset, from, to, compare))))
            .WithName("GetAnalyticsOperations")
            .RequirePermission(PermissionConstants.Analytics.View);

        group.MapGet("export", async Task<IResult> (
            [FromQuery] string section,
            [FromQuery] string format,
            [FromQuery] string? preset,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? compare,
            ISender sender) =>
        {
            var result = await sender.Send(new ExportAnalyticsQuery(section, format, preset, from, to, compare));
            if (!result.IsSuccess)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Bad Request",
                    Detail = result.Error.Description,
                    Type = result.Error.Code,
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            var export = result.Value;
            return Results.File(export.Content, export.ContentType, export.FileName);
        })
            .WithName("ExportAnalytics")
            .RequirePermission(PermissionConstants.Analytics.Export);
    }

    private static IResult MapResult<T>(Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });
}
