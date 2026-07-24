using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Collections;
using HAMBOX.Modules.Catalog.Application.Features.Collections.CreateCollection;
using HAMBOX.Modules.Catalog.Application.Features.Collections.DeleteCollection;
using HAMBOX.Modules.Catalog.Application.Features.Collections.GetCollectionById;
using HAMBOX.Modules.Catalog.Application.Features.Collections.GetCollections;
using HAMBOX.Modules.Catalog.Application.Features.Collections.GetCollectionTree;
using HAMBOX.Modules.Catalog.Application.Features.Collections.ReorderCollections;
using HAMBOX.Modules.Catalog.Application.Features.Collections.UpdateCollection;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Catalog.Presentation.Endpoints;

/// <summary>
/// Registers the (internal, owner-only) collection endpoints.
/// </summary>
internal static class CollectionEndpoints
{
    public static void MapCollectionEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/collections")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Collections")
            .HasApiVersion(1);

        // GET /api/v1/collections
        group.MapGet("", async Task<Results<Ok<PagedResult<CollectionDto>>, BadRequest<ProblemDetails>>> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;
            var query = new GetCollectionsQuery(pageNumber, pageSize, searchTerm);
            var result = await sender.Send(query);

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("GetCollections")
        .RequirePermission(PermissionConstants.Catalog.Collections.View);

        // GET /api/v1/collections/tree
        group.MapGet("tree", async Task<Results<Ok<IReadOnlyList<CollectionTreeItemDto>>, BadRequest<ProblemDetails>>> (ISender sender) =>
        {
            var result = await sender.Send(new GetCollectionTreeQuery());

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("GetCollectionTree")
        .RequirePermission(PermissionConstants.Catalog.Collections.View);

        // GET /api/v1/collections/{id}
        group.MapGet("{id:guid}", async Task<Results<Ok<CollectionDto>, NotFound<ProblemDetails>>> (Guid id, ISender sender) =>
        {
            var query = new GetCollectionByIdQuery(id);
            var result = await sender.Send(query);

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status404NotFound
            });
        })
        .WithName("GetCollectionById")
        .RequirePermission(PermissionConstants.Catalog.Collections.View);

        // POST /api/v1/collections
        group.MapPost("", async Task<Results<Created<Guid>, BadRequest<ProblemDetails>>> ([FromBody] CreateCollectionRequest request, ISender sender) =>
        {
            var command = new CreateCollectionCommand(
                request.Name, request.Description, request.Color, request.Icon, request.ParentId, request.SortOrder);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                return TypedResults.Created($"/api/v1/collections/{result.Value}", result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("CreateCollection")
        .RequirePermission(PermissionConstants.Catalog.Collections.Create);

        // PUT /api/v1/collections/{id}
        group.MapPut("{id:guid}", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (Guid id, [FromBody] UpdateCollectionRequest request, ISender sender) =>
        {
            var command = new UpdateCollectionCommand(
                id, request.Name, request.Description, request.Color, request.Icon, request.ParentId, request.SortOrder);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                return TypedResults.NoContent();
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("UpdateCollection")
        .RequirePermission(PermissionConstants.Catalog.Collections.Edit);

        // PUT /api/v1/collections/reorder
        group.MapPut("reorder", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (
            [FromBody] ReorderCollectionsRequest request,
            ISender sender) =>
        {
            var command = new ReorderCollectionsCommand(
                request.Entries.Select(e => new CollectionReorderEntry(e.Id, e.ParentId, e.SortOrder)).ToList());
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                return TypedResults.NoContent();
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("ReorderCollections")
        .RequirePermission(PermissionConstants.Catalog.Collections.Edit);

        // DELETE /api/v1/collections/{id}
        group.MapDelete("{id:guid}", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (Guid id, ISender sender) =>
        {
            var command = new DeleteCollectionCommand(id);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                return TypedResults.NoContent();
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("DeleteCollection")
        .RequirePermission(PermissionConstants.Catalog.Collections.Delete);

        group.MapPost("{id:guid}/restore", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new RestoreCollectionCommand(id));
            if (result.IsSuccess)
            {
                return TypedResults.NoContent();
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("RestoreCollection")
        .RequirePermission(PermissionConstants.Catalog.Collections.Edit);
    }
}

internal sealed record CreateCollectionRequest(string Name, string? Description, string? Color, string? Icon, Guid? ParentId, int SortOrder);
internal sealed record UpdateCollectionRequest(string Name, string? Description, string? Color, string? Icon, Guid? ParentId, int SortOrder);
internal sealed record ReorderCollectionsRequest(IReadOnlyList<CollectionReorderEntryRequest> Entries);
internal sealed record CollectionReorderEntryRequest(Guid Id, Guid? ParentId, int SortOrder);
