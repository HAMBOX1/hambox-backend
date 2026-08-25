using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.GetAdminReferrals;
using HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.GetAdminReferralById;
using HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.ReverseAdminReferral;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class AdminReferralEndpoints
{
    public static void MapAdminReferralEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/admin/referrals")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Admin Referrals")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            ISender sender) =>
        {
            var result = await sender.Send(new GetAdminReferralsQuery(
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 20 : pageSize,
                searchTerm,
                status));
            return MapResult(result);
        })
        .WithName("GetAdminReferrals")
        .RequirePermission(PermissionConstants.Referral.View);

        group.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetAdminReferralByIdQuery(id))))
        .WithName("GetAdminReferralById")
        .RequirePermission(PermissionConstants.Referral.View);

        group.MapPost("{id:guid}/reverse", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new ReverseAdminReferralCommand(id))))
        .WithName("ReverseAdminReferral")
        .RequirePermission(PermissionConstants.Referral.Manage);
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
