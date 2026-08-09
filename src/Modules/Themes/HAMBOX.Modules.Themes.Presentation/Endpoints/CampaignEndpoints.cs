using Asp.Versioning.Builder;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Themes.Application.Contracts.Campaigns;
using HAMBOX.Modules.Themes.Application.Features.Campaigns;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Themes.Presentation.Endpoints;

internal static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var admin = app.MapGroup("api/v{version:apiVersion}/campaigns")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Campaigns")
            .HasApiVersion(1)
            .RequireAuthorization();

        admin.MapGet("", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : pageSize;
            return MapResult(await sender.Send(new GetCampaignsQuery(pageNumber, pageSize, searchTerm, status)));
        }).RequirePermission(PermissionConstants.Campaigns.View);

        admin.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetCampaignByIdQuery(id))))
            .RequirePermission(PermissionConstants.Campaigns.View);

        admin.MapGet("{id:guid}/history", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetCampaignHistoryQuery(id))))
            .RequirePermission(PermissionConstants.Campaigns.View);

        admin.MapPost("", async Task<IResult> ([FromBody] CreateCampaignRequest request, ISender sender) =>
            MapResult(await sender.Send(new CreateCampaignCommand(request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Campaigns.Create);

        admin.MapPut("{id:guid}", async Task<IResult> (Guid id, [FromBody] UpdateCampaignRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateCampaignCommand(id, request))))
            .RequirePermission(PermissionConstants.Campaigns.Edit);

        admin.MapPost("{id:guid}/publish", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new PublishCampaignCommand(id))))
            .RequirePermission(PermissionConstants.Campaigns.Publish);

        admin.MapPost("{id:guid}/enable", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new EnableCampaignCommand(id))))
            .RequirePermission(PermissionConstants.Campaigns.Edit);

        admin.MapPost("{id:guid}/disable", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DisableCampaignCommand(id))))
            .RequirePermission(PermissionConstants.Campaigns.Edit);

        admin.MapPost("{id:guid}/archive", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new ArchiveCampaignCommand(id))))
            .RequirePermission(PermissionConstants.Campaigns.Edit);

        admin.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteCampaignCommand(id)), StatusCodes.Status204NoContent))
            .RequirePermission(PermissionConstants.Campaigns.Delete);

        // Preview reuses the existing Theme preview mechanism directly (POST
        // /api/v{version}/themes/{themeId}/preview, GET /api/v{version}/themes/preview/{token}) —
        // a campaign has no visual identity independent of the theme it points to, so there is no
        // separate campaign preview endpoint here.
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}
