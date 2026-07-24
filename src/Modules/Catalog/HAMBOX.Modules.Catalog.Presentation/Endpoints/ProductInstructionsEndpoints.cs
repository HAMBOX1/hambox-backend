using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Instructions.GetProductInstructions;
using HAMBOX.Modules.Catalog.Application.Features.Instructions.PublishProductInstructions;
using HAMBOX.Modules.Catalog.Application.Features.Instructions.SaveProductInstructions;
using HAMBOX.Modules.Catalog.Application.Features.Instructions.UnpublishProductInstructions;
using HAMBOX.Modules.Catalog.Application.Features.Instructions.UploadProductInstructionsImage;
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
/// Registers admin endpoints for authoring a product's private post-purchase documentation.
/// </summary>
internal static class ProductInstructionsEndpoints
{
    public static void MapProductInstructionsEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/products/{productId:guid}/instructions")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Product Instructions")
            .HasApiVersion(1);

        group.MapGet("", async Task<Results<Ok<ProductInstructionsDto>, NotFound<ProblemDetails>>> (
            Guid productId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetProductInstructionsQuery(productId));

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
        .WithName("GetProductInstructions")
        .RequirePermission(PermissionConstants.Catalog.Products.View);

        group.MapPut("", async Task<Results<Ok<ProductInstructionsDto>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> (
            Guid productId,
            [FromBody] SaveProductInstructionsRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new SaveProductInstructionsCommand(productId, request.Title, request.ContentHtml));

            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            if (result.Error == CatalogErrors.ProductNotFound)
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
        .WithName("SaveProductInstructions")
        .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        group.MapPost("publish", async Task<Results<Ok<ProductInstructionsDto>, BadRequest<ProblemDetails>>> (
            Guid productId,
            ISender sender) => await PublishOrUnpublish(sender, new PublishProductInstructionsCommand(productId)))
        .WithName("PublishProductInstructions")
        .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        group.MapPost("unpublish", async Task<Results<Ok<ProductInstructionsDto>, BadRequest<ProblemDetails>>> (
            Guid productId,
            ISender sender) => await PublishOrUnpublish(sender, new UnpublishProductInstructionsCommand(productId)))
        .WithName("UnpublishProductInstructions")
        .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        group.MapPost("images", async Task<Results<Ok<ProductInstructionsImageResponse>, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> (
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
            var command = new UploadProductInstructionsImageCommand(
                productId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length);

            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                return TypedResults.Ok(new ProductInstructionsImageResponse(result.Value));
            }

            if (result.Error == CatalogErrors.ProductNotFound)
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
        .WithName("UploadProductInstructionsImage")
        .RequirePermission(PermissionConstants.Catalog.Products.Edit)
        .DisableAntiforgery();
    }

    private static async Task<Results<Ok<ProductInstructionsDto>, BadRequest<ProblemDetails>>> PublishOrUnpublish(
        ISender sender,
        IRequest<Result<ProductInstructionsDto>> command)
    {
        var result = await sender.Send(command);

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
    }
}

internal sealed record SaveProductInstructionsRequest(string Title, string ContentHtml);

internal sealed record ProductInstructionsImageResponse(string Url);
