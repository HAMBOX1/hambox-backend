using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Support.Application.Features.SavedReplies.CreateSavedReply;
using HAMBOX.Modules.Support.Application.Features.SavedReplies.CreateSavedReplyFolder;
using HAMBOX.Modules.Support.Application.Features.SavedReplies.DeleteSavedReply;
using HAMBOX.Modules.Support.Application.Features.SavedReplies.DeleteSavedReplyFolder;
using HAMBOX.Modules.Support.Application.Features.SavedReplies.GetSavedReplies;
using HAMBOX.Modules.Support.Application.Features.SavedReplies.GetSavedReplyFolders;
using HAMBOX.Modules.Support.Application.Features.SavedReplies.RenderSavedReply;
using HAMBOX.Modules.Support.Application.Features.SavedReplies.UpdateSavedReply;
using HAMBOX.Modules.Support.Application.Features.SavedReplies.UpdateSavedReplyFolder;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Support.Presentation.Endpoints;

internal static class SavedReplyEndpoints
{
    public static void MapSavedReplyEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        // Search/render: any agent replying to a ticket needs these — gated by Reply, not the
        // narrower ManageSavedReplies (authoring) permission.
        var useGroup = app.MapGroup("api/v{version:apiVersion}/support/saved-replies")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Support Admin")
            .HasApiVersion(1)
            .RequirePermission(PermissionConstants.Support.Reply);

        useGroup.MapGet("", async Task<IResult> ([FromQuery] Guid? folderId, [FromQuery] string? search, ISender sender) =>
            MapResult(await sender.Send(new GetSavedRepliesQuery(folderId, search))));

        useGroup.MapGet("folders", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetSavedReplyFoldersQuery())));

        useGroup.MapPost("{id:guid}/render", async Task<IResult> (Guid id, [FromQuery] Guid ticketId, ISender sender) =>
            MapResult(await sender.Send(new RenderSavedReplyQuery(id, ticketId))));

        var manageGroup = app.MapGroup("api/v{version:apiVersion}/support/admin/saved-replies")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Support Admin")
            .HasApiVersion(1)
            .RequirePermission(PermissionConstants.Support.ManageSavedReplies);

        manageGroup.MapPost("", async Task<IResult> ([FromBody] CreateSavedReplyCommand command, ISender sender) =>
            MapResult(await sender.Send(command), StatusCodes.Status201Created));

        manageGroup.MapPut("{id:guid}", async Task<IResult> (
            Guid id, [FromBody] UpdateReplyRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateSavedReplyCommand(id, request.FolderId, request.Title, request.Body))));

        manageGroup.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteSavedReplyCommand(id)), StatusCodes.Status204NoContent));

        manageGroup.MapPost("folders", async Task<IResult> ([FromBody] CreateSavedReplyFolderCommand command, ISender sender) =>
            MapResult(await sender.Send(command), StatusCodes.Status201Created));

        manageGroup.MapPut("folders/{id:guid}", async Task<IResult> (
            Guid id, [FromBody] UpdateFolderRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateSavedReplyFolderCommand(id, request.Name, request.SortOrder))));

        manageGroup.MapDelete("folders/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteSavedReplyFolderCommand(id)), StatusCodes.Status204NoContent));
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}

internal sealed record UpdateReplyRequest(Guid? FolderId, string Title, string Body);

internal sealed record UpdateFolderRequest(string Name, int SortOrder);
