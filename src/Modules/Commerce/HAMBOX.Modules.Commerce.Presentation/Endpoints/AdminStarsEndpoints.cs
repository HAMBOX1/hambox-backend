using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Cosmetics;
using HAMBOX.Modules.Commerce.Application.Features.Missions;
using HAMBOX.Modules.Commerce.Application.Features.Rewards;
using HAMBOX.Modules.Commerce.Application.Features.Stars;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class AdminStarsEndpoints
{
    public static void MapAdminStarsEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/admin/stars")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Admin Stars")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("rules", async Task<IResult> (ISender sender) => MapResult(await sender.Send(new GetStarsRulesQuery())))
            .WithName("GetStarsRules")
            .RequirePermission(PermissionConstants.Stars.View);

        group.MapPost("rules", async Task<IResult> (StarsRuleInput input, ISender sender) =>
                MapResult(await sender.Send(new CreateStarsRuleCommand(input))))
            .WithName("CreateStarsRule")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapPut("rules/{id:guid}", async Task<IResult> (Guid id, StarsRuleInput input, ISender sender) =>
                MapResult(await sender.Send(new UpdateStarsRuleCommand(id, input))))
            .WithName("UpdateStarsRule")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapPost("rules/preview", async Task<IResult> (PreviewRequest body, ISender sender) =>
                MapResult(await sender.Send(new PreviewStarsRuleQuery(body.Scope, body.ScopeId, body.Mode, body.Value))))
            .WithName("PreviewStarsRule")
            .RequirePermission(PermissionConstants.Stars.View);

        group.MapPost("rules/{id:guid}/active", async Task<IResult> (Guid id, SetActiveRequest body, ISender sender) =>
                MapResult(await sender.Send(new SetStarsRuleActiveCommand(id, body.IsActive))))
            .WithName("SetStarsRuleActive")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapDelete("rules/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new DeleteStarsRuleCommand(id));
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
            })
            .WithName("DeleteStarsRule")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapGet("rewards", async Task<IResult> (ISender sender) => MapResult(await sender.Send(new GetAdminRewardsQuery())))
            .WithName("GetAdminRewards")
            .RequirePermission(PermissionConstants.Stars.View);

        group.MapPost("rewards", async Task<IResult> (RewardInput input, ISender sender) =>
                MapResult(await sender.Send(new SaveRewardCommand(null, input))))
            .WithName("CreateReward")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapPut("rewards/{id:guid}", async Task<IResult> (Guid id, RewardInput input, ISender sender) =>
                MapResult(await sender.Send(new SaveRewardCommand(id, input))))
            .WithName("UpdateReward")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapDelete("rewards/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new DeleteRewardCommand(id));
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
            })
            .WithName("DeleteReward")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapGet("redemptions", async Task<IResult> ([FromQuery] string? status, ISender sender) =>
                MapResult(await sender.Send(new GetAdminRedemptionsQuery(status))))
            .WithName("GetAdminRedemptions")
            .RequirePermission(PermissionConstants.Stars.View);

        group.MapGet("missions", async Task<IResult> (ISender sender) => MapResult(await sender.Send(new GetAdminMissionsQuery())))
            .WithName("GetAdminMissions")
            .RequirePermission(PermissionConstants.Stars.View);

        group.MapPost("missions", async Task<IResult> (MissionInput input, ISender sender) =>
                MapResult(await sender.Send(new SaveMissionCommand(null, input))))
            .WithName("CreateMission")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapPut("missions/{id:guid}", async Task<IResult> (Guid id, MissionInput input, ISender sender) =>
                MapResult(await sender.Send(new SaveMissionCommand(id, input))))
            .WithName("UpdateMission")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapDelete("missions/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new DeleteMissionCommand(id));
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
            })
            .WithName("DeleteMission")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapGet("cosmetics", async Task<IResult> (ISender sender) => MapResult(await sender.Send(new GetAdminCosmeticsQuery())))
            .WithName("GetAdminCosmetics")
            .RequirePermission(PermissionConstants.Stars.View);

        group.MapPost("cosmetics", async Task<IResult> (CosmeticInput input, ISender sender) =>
                MapResult(await sender.Send(new SaveCosmeticCommand(null, input))))
            .WithName("CreateCosmetic")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapPut("cosmetics/{id:guid}", async Task<IResult> (Guid id, CosmeticInput input, ISender sender) =>
                MapResult(await sender.Send(new SaveCosmeticCommand(id, input))))
            .WithName("UpdateCosmetic")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapDelete("cosmetics/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new DeleteCosmeticCommand(id));
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
            })
            .WithName("DeleteCosmetic")
            .RequirePermission(PermissionConstants.Stars.ManageRules);

        group.MapGet("accounts/{userId}", async Task<IResult> (
                string userId, [FromQuery] int pageNumber, [FromQuery] int pageSize, [FromQuery] string? type, ISender sender) =>
                MapResult(await sender.Send(new GetAdminStarsAccountQuery(
                    userId, pageNumber <= 0 ? 1 : pageNumber, pageSize <= 0 ? 20 : pageSize, type))))
            .WithName("GetAdminStarsAccount")
            .RequirePermission(PermissionConstants.Stars.View);

        group.MapPost("accounts/{userId}/adjust", async Task<IResult> (string userId, AdjustRequest body, ISender sender) =>
                MapResult(await sender.Send(new AdjustStarsCommand(userId, body.Points, body.Reason))))
            .WithName("AdjustStars")
            .RequirePermission(PermissionConstants.Stars.Adjust);
    }

    private static IResult MapResult<T>(Result<T> result) => result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    private static IResult Problem(HAMBOX.SharedKernel.Errors.Error error) =>
        Results.BadRequest(new ProblemDetails
        {
            Title = "Bad Request",
            Detail = error.Description,
            Type = error.Code,
            Status = StatusCodes.Status400BadRequest,
        });

    internal sealed record SetActiveRequest(bool IsActive);

    internal sealed record PreviewRequest(HAMBOX.Modules.Commerce.Domain.Account.StarsRuleScope Scope, Guid? ScopeId, HAMBOX.Modules.Commerce.Domain.Account.StarsRuleMode Mode, decimal Value);

    internal sealed record AdjustRequest(int Points, string Reason);
}
