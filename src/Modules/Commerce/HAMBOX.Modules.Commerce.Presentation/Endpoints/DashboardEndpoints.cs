using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Dashboard.GetAdminDashboard;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        app.MapGet("api/v{version:apiVersion}/dashboard", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetAdminDashboardQuery())))
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Dashboard")
            .HasApiVersion(1)
            .WithName("GetAdminDashboard")
            .RequireAuthorization()
            .RequirePermission(PermissionConstants.Dashboard.View);
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
