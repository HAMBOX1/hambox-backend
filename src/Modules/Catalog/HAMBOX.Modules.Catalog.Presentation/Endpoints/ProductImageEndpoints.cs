using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Products.Images.DeleteProductImage;
using HAMBOX.Modules.Catalog.Application.Features.Products.Images.GetProductImages;
using HAMBOX.Modules.Catalog.Application.Features.Products.Images.ReorderProductImages;
using HAMBOX.Modules.Catalog.Application.Features.Products.Images.SetPrimaryProductImage;
using HAMBOX.Modules.Catalog.Application.Features.Products.Images.UploadProductImage;
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
/// Registers product image endpoints.
/// </summary>
internal static class ProductImageEndpoints
{
    public static void MapProductImageEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/products/{productId:guid}/images")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Product Images")
            .HasApiVersion(1);

        group.MapGet("", async Task<Results<Ok<IReadOnlyList<ProductImageDto>>, NotFound<ProblemDetails>>> (
            Guid productId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetProductImagesQuery(productId));

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
        .WithName("GetProductImages")
        .AllowAnonymous();

        group.MapPost("", async Task<Results<Created<ProductImageDto>, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> (
            Guid productId,
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
            var command = new UploadProductImageCommand(
                productId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length);

            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                return TypedResults.Created(
                    $"/api/v1/products/{productId}/images/{result.Value.Id}",
                    result.Value);
            }

            if (result.Error == Catalog.Application.Errors.CatalogErrors.ProductNotFound)
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
        .WithName("UploadProductImage")
        .RequirePermission(PermissionConstants.Products.Update)
        .DisableAntiforgery();

        group.MapDelete("{imageId:guid}", async Task<Results<NoContent, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> (
            Guid productId,
            Guid imageId,
            ISender sender) =>
        {
            var result = await sender.Send(new DeleteProductImageCommand(productId, imageId));

            if (result.IsSuccess)
            {
                return TypedResults.NoContent();
            }

            if (result.Error == Catalog.Application.Errors.CatalogErrors.ProductNotFound
                || result.Error == Catalog.Application.Errors.CatalogErrors.ProductImageNotFound)
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
        .WithName("DeleteProductImage")
        .RequirePermission(PermissionConstants.Products.Update);

        group.MapPut("{imageId:guid}/primary", async Task<Results<Ok<ProductImageDto>, NotFound<ProblemDetails>>> (
            Guid productId,
            Guid imageId,
            ISender sender) =>
        {
            var result = await sender.Send(new SetPrimaryProductImageCommand(productId, imageId));

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
        .WithName("SetPrimaryProductImage")
        .RequirePermission(PermissionConstants.Products.Update);

        group.MapPut("reorder", async Task<Results<Ok<IReadOnlyList<ProductImageDto>>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> (
            Guid productId,
            [FromBody] ReorderProductImagesRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new ReorderProductImagesCommand(productId, request.OrderedImageIds));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            if (result.Error == Catalog.Application.Errors.CatalogErrors.ProductNotFound)
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
        .WithName("ReorderProductImages")
        .RequirePermission(PermissionConstants.Products.Update);
    }
}

internal sealed record ReorderProductImagesRequest(IReadOnlyList<Guid> OrderedImageIds);
