using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Categories;
using HAMBOX.Modules.Catalog.Application.Features.Categories.CreateCategory;
using HAMBOX.Modules.Catalog.Application.Features.Categories.DeleteCategory;
using HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategories;
using HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryById;
using HAMBOX.Modules.Catalog.Application.Features.Categories.UpdateCategory;
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
/// Registers the category endpoints.
/// </summary>
internal static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/categories")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Categories")
            .HasApiVersion(1);

        // GET /api/v1/categories
        group.MapGet("", async Task<Results<Ok<PagedResult<CategoryDto>>, BadRequest<ProblemDetails>>> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] bool? activeOnly,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;
            var query = new GetCategoriesQuery(pageNumber, pageSize, searchTerm, activeOnly);
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
        .WithName("GetCategories")
        .AllowAnonymous();

        // GET /api/v1/categories/{id}
        group.MapGet("{id:guid}", async Task<Results<Ok<CategoryDto>, NotFound<ProblemDetails>>> (Guid id, ISender sender) =>
        {
            var query = new GetCategoryByIdQuery(id);
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
        .WithName("GetCategoryById")
        .AllowAnonymous();

        // POST /api/v1/categories
        group.MapPost("", async Task<Results<Created<Guid>, BadRequest<ProblemDetails>>> ([FromBody] CreateCategoryRequest request, ISender sender) =>
        {
            var command = new CreateCategoryCommand(request.NameAr, request.NameEn, request.Slug, request.ParentId);
            var result = await sender.Send(command);
            
            if (result.IsSuccess)
            {
                return TypedResults.Created($"/api/v1/categories/{result.Value}", result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("CreateCategory")
        .RequirePermission(PermissionConstants.Catalog.Categories.Create);

        // PUT /api/v1/categories/{id}
        group.MapPut("{id:guid}", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (Guid id, [FromBody] UpdateCategoryRequest request, ISender sender) =>
        {
            var command = new UpdateCategoryCommand(id, request.NameAr, request.NameEn, request.Slug, request.IsActive, request.ParentId);
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
        .WithName("UpdateCategory")
        .RequirePermission(PermissionConstants.Catalog.Categories.Edit);

        // DELETE /api/v1/categories/{id}
        group.MapDelete("{id:guid}", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (Guid id, ISender sender) =>
        {
            var command = new DeleteCategoryCommand(id);
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
        .WithName("DeleteCategory")
        .RequirePermission(PermissionConstants.Catalog.Categories.Delete);

        group.MapPost("{id:guid}/restore", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new RestoreCategoryCommand(id));
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
        .WithName("RestoreCategory")
        .RequirePermission(PermissionConstants.Catalog.Categories.Edit);
    }
}

internal sealed record CreateCategoryRequest(string NameAr, string NameEn, string Slug, Guid? ParentId);
internal sealed record UpdateCategoryRequest(string NameAr, string NameEn, string Slug, bool IsActive, Guid? ParentId);
