using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Loyalty;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class AdminLoyaltyEndpoints
{
    public static void MapAdminLoyaltyEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/admin/loyalty")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Admin Loyalty")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("ranks", async Task<IResult> (ISender sender) => MapResult(await sender.Send(new GetLoyaltyRanksQuery())))
            .WithName("GetLoyaltyRanks")
            .RequirePermission(PermissionConstants.Loyalty.View);

        group.MapPut("ranks/{id:guid}", async Task<IResult> (Guid id, UpdateLoyaltyRankInput input, ISender sender) =>
                MapResult(await sender.Send(new UpdateLoyaltyRankCommand(id, input))))
            .WithName("UpdateLoyaltyRank")
            .RequirePermission(PermissionConstants.Loyalty.Manage);

        group.MapPost("recalculate", async Task<IResult> (ISender sender) => MapResult(await sender.Send(new RecalculateLoyaltyCommand())))
            .WithName("RecalculateLoyaltyRanks")
            .RequirePermission(PermissionConstants.Loyalty.Manage);

        group.MapGet("customers/{userId}", async Task<IResult> (string userId, ISender sender) =>
                MapResult(await sender.Send(new GetCustomerLoyaltyQuery(userId))))
            .WithName("GetCustomerLoyalty")
            .RequirePermission(PermissionConstants.Loyalty.View);

        group.MapPost("customers/{userId}/rank", async Task<IResult> (string userId, SetRankRequest body, ISender sender) =>
                MapResult(await sender.Send(new SetCustomerRankCommand(userId, body.RankKey, body.Reason))))
            .WithName("SetCustomerLoyaltyRank")
            .RequirePermission(PermissionConstants.Loyalty.AdjustRank);
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

    internal sealed record SetRankRequest(string? RankKey, string Reason);
}
