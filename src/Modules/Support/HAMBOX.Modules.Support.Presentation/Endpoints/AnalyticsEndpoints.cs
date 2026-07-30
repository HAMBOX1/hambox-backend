using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Support.Application.Features.Analytics.GetSupportStatistics;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Support.Presentation.Endpoints;

internal static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/support/admin/analytics")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Support Admin")
            .HasApiVersion(1)
            .RequirePermission(PermissionConstants.Support.ViewAnalytics);

        group.MapGet("", async Task<IResult> (
            [FromQuery] DateTimeOffset? dateFrom, [FromQuery] DateTimeOffset? dateTo, ISender sender) =>
            MapResult(await sender.Send(new GetSupportStatisticsQuery(dateFrom, dateTo))));
    }

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}
