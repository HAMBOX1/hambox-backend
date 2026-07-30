using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Support.Application.Features.Categories.CreateTicketCategory;
using HAMBOX.Modules.Support.Application.Features.Categories.DeleteTicketCategory;
using HAMBOX.Modules.Support.Application.Features.Categories.GetTicketCategories;
using HAMBOX.Modules.Support.Application.Features.Categories.UpdateTicketCategory;
using HAMBOX.Modules.Support.Application.Features.Priorities.CreateTicketPriority;
using HAMBOX.Modules.Support.Application.Features.Priorities.DeleteTicketPriority;
using HAMBOX.Modules.Support.Application.Features.Priorities.GetTicketPriorities;
using HAMBOX.Modules.Support.Application.Features.Priorities.UpdateTicketPriority;
using HAMBOX.Modules.Support.Application.Features.Tags.CreateTicketTag;
using HAMBOX.Modules.Support.Application.Features.Tags.DeleteTicketTag;
using HAMBOX.Modules.Support.Application.Features.Tags.GetTicketTags;
using HAMBOX.Modules.Support.Application.Features.Tags.UpdateTicketTag;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Support.Presentation.Endpoints;

internal static class LookupEndpoints
{
    public static void MapLookupEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        // Read-only lookups: any authenticated user (customer creating a ticket, or an agent
        // filtering the inbox) needs these — not permission-gated.
        var readGroup = app.MapGroup("api/v{version:apiVersion}/support")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Support Lookups")
            .HasApiVersion(1)
            .RequireAuthorization();

        readGroup.MapGet("categories", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetTicketCategoriesQuery())));

        readGroup.MapGet("priorities", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetTicketPrioritiesQuery())));

        readGroup.MapGet("tags", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetTicketTagsQuery())))
            .RequirePermission(PermissionConstants.Support.View);

        var adminGroup = app.MapGroup("api/v{version:apiVersion}/support/admin")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Support Admin")
            .HasApiVersion(1)
            .RequirePermission(PermissionConstants.Support.ManageCategories);

        adminGroup.MapPost("categories", async Task<IResult> ([FromBody] CreateTicketCategoryCommand command, ISender sender) =>
            MapResult(await sender.Send(command), StatusCodes.Status201Created));

        adminGroup.MapPut("categories/{id:guid}", async Task<IResult> (
            Guid id, [FromBody] UpdateCategoryRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateTicketCategoryCommand(
                id, request.Name, request.Color, request.Icon, request.SortOrder, request.IsActive))));

        adminGroup.MapDelete("categories/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteTicketCategoryCommand(id)), StatusCodes.Status204NoContent));

        adminGroup.MapPost("priorities", async Task<IResult> ([FromBody] CreateTicketPriorityCommand command, ISender sender) =>
            MapResult(await sender.Send(command), StatusCodes.Status201Created));

        adminGroup.MapPut("priorities/{id:guid}", async Task<IResult> (
            Guid id, [FromBody] UpdatePriorityRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateTicketPriorityCommand(
                id, request.Name, request.Color, request.Level,
                request.SlaFirstResponseMinutes, request.SlaResolutionMinutes, request.IsActive))));

        adminGroup.MapDelete("priorities/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteTicketPriorityCommand(id)), StatusCodes.Status204NoContent));

        adminGroup.MapPost("tags", async Task<IResult> ([FromBody] CreateTicketTagCommand command, ISender sender) =>
            MapResult(await sender.Send(command), StatusCodes.Status201Created));

        adminGroup.MapPut("tags/{id:guid}", async Task<IResult> (
            Guid id, [FromBody] UpdateTagRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateTicketTagCommand(id, request.Name, request.Color))));

        adminGroup.MapDelete("tags/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteTicketTagCommand(id)), StatusCodes.Status204NoContent));
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}

internal sealed record UpdateCategoryRequest(string Name, string Color, string Icon, int SortOrder, bool IsActive);

internal sealed record UpdatePriorityRequest(
    string Name, string Color, int Level, int? SlaFirstResponseMinutes, int? SlaResolutionMinutes, bool IsActive);

internal sealed record UpdateTagRequest(string Name, string Color);
