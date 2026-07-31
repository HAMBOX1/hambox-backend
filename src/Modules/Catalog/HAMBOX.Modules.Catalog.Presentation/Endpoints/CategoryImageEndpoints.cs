using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Categories.Images.RemoveCategoryImage;
using HAMBOX.Modules.Catalog.Application.Features.Categories.Images.UploadCategoryImage;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Catalog.Presentation.Endpoints;

/// <summary>
/// Registers category image endpoints.
/// </summary>
internal static class CategoryImageEndpoints
{
    public static void MapCategoryImageEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/categories/{categoryId:guid}/image")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Category Images")
            .HasApiVersion(1);

        group.MapPost("", async Task<Results<Ok<CategoryImageDto>, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> (
            Guid categoryId,
            IFormFile file,
            ISender sender) =>
        {
            if (file.Length <= 0)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Bad Request",
                    Detail = "An image file is required.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            await using var stream = file.OpenReadStream();
            var command = new UploadCategoryImageCommand(
                categoryId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length);

            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            if (result.Error == Catalog.Application.Errors.CatalogErrors.CategoryNotFound)
            {
                return TypedResults.NotFound(new ProblemDetails
                {
                    Title = "Not Found",
                    Detail = result.Error.Description,
                    Type = result.Error.Code,
                    Status = StatusCodes.Status404NotFound
                });
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("UploadCategoryImage")
        .RequirePermission(PermissionConstants.Catalog.Categories.Edit)
        .DisableAntiforgery();

        group.MapDelete("", async Task<Results<NoContent, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> (
            Guid categoryId,
            ISender sender) =>
        {
            var result = await sender.Send(new RemoveCategoryImageCommand(categoryId));

            if (result.IsSuccess)
            {
                return TypedResults.NoContent();
            }

            if (result.Error == Catalog.Application.Errors.CatalogErrors.CategoryNotFound
                || result.Error == Catalog.Application.Errors.CatalogErrors.CategoryImageNotFound)
            {
                return TypedResults.NotFound(new ProblemDetails
                {
                    Title = "Not Found",
                    Detail = result.Error.Description,
                    Type = result.Error.Code,
                    Status = StatusCodes.Status404NotFound
                });
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("RemoveCategoryImage")
        .RequirePermission(PermissionConstants.Catalog.Categories.Edit);
    }
}
