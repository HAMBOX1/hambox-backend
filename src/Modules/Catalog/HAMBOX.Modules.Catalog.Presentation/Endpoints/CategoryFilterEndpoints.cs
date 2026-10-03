using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Features.Categories.CategoryFilters;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Catalog.Presentation.Endpoints;

/// <summary>
/// Admin endpoints for the per-category storefront filter lists. The id <c>00000000-0000-0000-0000-000000000000</c>
/// addresses the store-wide default list. Anyone with the category permissions can use these, so the owner can
/// delegate filter management to an admin simply by granting that role the category permissions.
/// </summary>
internal static class CategoryFilterEndpoints
{
    public static void MapCategoryFilterEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/category-filters")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Category Filters")
            .HasApiVersion(1);

        group.MapGet("{categoryId:guid}", async (Guid categoryId, ISender sender) =>
        {
            var result = await sender.Send(new GetCategoryFilterConfigQuery(categoryId));
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error.Description });
        })
        .WithName("GetCategoryFilterConfig")
        .RequirePermission(PermissionConstants.Catalog.Categories.View);

        group.MapPut("{categoryId:guid}", async (
            Guid categoryId,
            [FromBody] SaveCategoryFiltersRequest body,
            ISender sender) =>
        {
            var result = await sender.Send(new SaveCategoryFilterConfigCommand(categoryId, body.Items));
            return result.IsSuccess
                ? Results.NoContent()
                : Results.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error.Description });
        })
        .WithName("SaveCategoryFilterConfig")
        .RequirePermission(PermissionConstants.Catalog.Categories.Edit);

        group.MapDelete("{categoryId:guid}", async (Guid categoryId, ISender sender) =>
        {
            var result = await sender.Send(new ResetCategoryFilterConfigCommand(categoryId));
            return result.IsSuccess
                ? Results.NoContent()
                : Results.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error.Description });
        })
        .WithName("ResetCategoryFilterConfig")
        .RequirePermission(PermissionConstants.Catalog.Categories.Edit);
    }
}

internal sealed record SaveCategoryFiltersRequest(IReadOnlyList<CategoryFilterInput> Items);
