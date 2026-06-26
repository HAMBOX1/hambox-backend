using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Products.CreateProduct;
using HAMBOX.Modules.Catalog.Application.Features.Products.DeleteProduct;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts;
using HAMBOX.Modules.Catalog.Application.Features.Products.UpdateProduct;
using HAMBOX.Modules.Catalog.Domain.Enums;
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
/// Registers the product endpoints.
/// </summary>
internal static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/products")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Products")
            .HasApiVersion(1);

        // GET /api/v1/products
        group.MapGet("", async Task<Results<Ok<PagedResult<ProductDto>>, BadRequest<ProblemDetails>>> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] ProductStatus? status,
            [FromQuery] Guid? categoryId,
            [FromQuery] ProductSortBy? sortBy,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;
            var query = new GetProductsQuery(pageNumber, pageSize, searchTerm, status, categoryId, sortBy);
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
        .WithName("GetProducts")
        .AllowAnonymous();

        // GET /api/v1/products/{id}
        group.MapGet("{id:guid}", async Task<Results<Ok<ProductDto>, NotFound<ProblemDetails>>> (Guid id, ISender sender) =>
        {
            var query = new GetProductByIdQuery(id);
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
        .WithName("GetProductById")
        .AllowAnonymous();

        // POST /api/v1/products
        group.MapPost("", async Task<Results<Created<Guid>, BadRequest<ProblemDetails>>> ([FromBody] CreateProductRequest request, ISender sender) =>
        {
            var command = new CreateProductCommand(request.NameAr, request.NameEn, request.DescriptionAr, request.DescriptionEn, request.Price, request.CategoryId);
            var result = await sender.Send(command);
            
            if (result.IsSuccess)
            {
                return TypedResults.Created($"/api/v1/products/{result.Value}", result.Value);
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest
            });
        })
        .WithName("CreateProduct")
        .RequirePermission(PermissionConstants.Products.Create);

        // PUT /api/v1/products/{id}
        group.MapPut("{id:guid}", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (Guid id, [FromBody] UpdateProductRequest request, ISender sender) =>
        {
            var command = new UpdateProductCommand(id, request.NameAr, request.NameEn, request.DescriptionAr, request.DescriptionEn, request.Price, request.CategoryId, request.Status);
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
        .WithName("UpdateProduct")
        .RequirePermission(PermissionConstants.Products.Update);

        // DELETE /api/v1/products/{id}
        group.MapDelete("{id:guid}", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (Guid id, ISender sender) =>
        {
            var command = new DeleteProductCommand(id);
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
        .WithName("DeleteProduct")
        .RequirePermission(PermissionConstants.Products.Delete);
    }
}

internal sealed record CreateProductRequest(string NameAr, string NameEn, string DescriptionAr, string DescriptionEn, decimal Price, Guid CategoryId);
internal sealed record UpdateProductRequest(string NameAr, string NameEn, string DescriptionAr, string DescriptionEn, decimal Price, Guid CategoryId, ProductStatus Status);
