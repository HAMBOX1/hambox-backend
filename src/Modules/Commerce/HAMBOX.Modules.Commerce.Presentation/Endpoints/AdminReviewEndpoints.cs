using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Account.Reviews.Admin;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class AdminReviewEndpoints
{
    public static void MapAdminReviewEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/admin/reviews")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Admin Reviews")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("", async Task<IResult> (
            [FromQuery] bool? hidden,
            [FromQuery] Guid? productId,
            [FromQuery] int? take,
            ISender sender) =>
            MapResult(await sender.Send(new GetAdminReviewsQuery(hidden, productId, take ?? 100))))
        .WithName("GetAdminReviews")
        .RequirePermission(PermissionConstants.Reviews.View);

        group.MapPost("{id:guid}/hide", async Task<IResult> (Guid id, [FromBody] HideReviewRequest request, ISender sender) =>
            MapResult(await sender.Send(new HideReviewCommand(id, request.Reason))))
        .WithName("HideReview")
        .RequirePermission(PermissionConstants.Reviews.Moderate);

        group.MapPost("{id:guid}/unhide", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new UnhideReviewCommand(id))))
        .WithName("UnhideReview")
        .RequirePermission(PermissionConstants.Reviews.Moderate);
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

internal sealed record HideReviewRequest(string Reason);
