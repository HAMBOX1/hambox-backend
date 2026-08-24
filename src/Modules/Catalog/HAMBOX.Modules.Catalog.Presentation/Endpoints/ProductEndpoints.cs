using System.Text.Json;
using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;
using HAMBOX.Modules.Catalog.Application.Features.Products.BulkDeleteProducts;
using HAMBOX.Modules.Catalog.Application.Features.Products.BulkDuplicateProducts;
using HAMBOX.Modules.Catalog.Application.Features.Products.BulkProducts;
using HAMBOX.Modules.Catalog.Application.Features.Products.ChangeProductCategory;
using HAMBOX.Modules.Catalog.Application.Features.Products.CreateProduct;
using HAMBOX.Modules.Catalog.Application.Features.Products.DeleteProduct;
using HAMBOX.Modules.Catalog.Application.Features.Products.ExportProducts;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductFacets;
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
            [FromQuery] string? attributes,
            [FromQuery] Guid? collectionId,
            [FromQuery] Guid[]? productIds,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;
            var query = new GetProductsQuery(
                pageNumber, pageSize, searchTerm, status, categoryId, sortBy, ParseAttributeFilters(attributes), collectionId,
                productIds is { Length: > 0 } ? productIds : null);
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

        // GET /api/v1/products/facets
        group.MapGet("facets", async Task<Results<Ok<IReadOnlyList<ProductFacetGroupDto>>, BadRequest<ProblemDetails>>> (
            [FromQuery] string? searchTerm,
            [FromQuery] Guid? categoryId,
            [FromQuery] string? attributes,
            ISender sender) =>
        {
            var query = new GetProductFacetsQuery(searchTerm, categoryId, ParseAttributeFilters(attributes));
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
        .WithName("GetProductFacets")
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
            var command = new CreateProductCommand(request.NameAr, request.NameEn, request.DescriptionAr, request.DescriptionEn, request.Price, request.CategoryId, request.AdditionalCategoryIds, request.CollectionIds, request.PublicReleaseOnUtc);
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
        .RequirePermission(PermissionConstants.Catalog.Products.Create);

        // PUT /api/v1/products/{id}
        group.MapPut("{id:guid}", async Task<Results<NoContent, BadRequest<ProblemDetails>>> (Guid id, [FromBody] UpdateProductRequest request, ISender sender) =>
        {
            var command = new UpdateProductCommand(id, request.NameAr, request.NameEn, request.DescriptionAr, request.DescriptionEn, request.Price, request.CategoryId, request.Status, request.AdditionalCategoryIds, request.CollectionIds, request.PublicReleaseOnUtc);
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
        .RequirePermission(PermissionConstants.Catalog.Products.Edit);

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
        .RequirePermission(PermissionConstants.Catalog.Products.Delete);

        group.MapPost("{id:guid}/publish", async (Guid id, ISender sender) =>
            await SendEmptyResult(sender, new PublishProductCommand(id)))
            .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        group.MapPost("{id:guid}/deactivate", async (Guid id, ISender sender) =>
            await SendEmptyResult(sender, new DeactivateProductCommand(id)))
            .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        group.MapPost("{id:guid}/archive", async (Guid id, ISender sender) =>
            await SendEmptyResult(sender, new ArchiveProductCommand(id)))
            .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        group.MapPost("{id:guid}/restore", async (Guid id, ISender sender) =>
            await SendEmptyResult(sender, new RestoreProductCommand(id)))
            .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        group.MapPost("{id:guid}/duplicate", async Task<Results<Ok<Guid>, BadRequest<ProblemDetails>>> (
            Guid id,
            [FromBody] DuplicateProductRequest? request,
            ISender sender) =>
        {
            var result = await sender.Send(new DuplicateProductCommand(id, request?.NameSuffix));
            if (result.IsSuccess)
            {
                return TypedResults.Ok(result.Value);
            }

            return TypedResults.BadRequest(Problem(result));
        })
        .WithName("DuplicateProduct")
        .RequirePermission(PermissionConstants.Catalog.Products.Create);

        // POST /api/v1/products/{id}/category
        group.MapPost("{id:guid}/category", async (Guid id, [FromBody] ChangeProductCategoryRequest request, ISender sender) =>
            await SendEmptyResult(sender, new ChangeProductCategoryCommand(id, request.CategoryId)))
            .WithName("ChangeProductCategory")
            .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        // POST /api/v1/products/{id}/price
        group.MapPost("{id:guid}/price", async (Guid id, [FromBody] AdjustProductPriceRequest request, ISender sender) =>
            await SendEmptyResult(sender, new AdjustProductPriceCommand(id, request.Mode, request.Value)))
            .WithName("AdjustProductPrice")
            .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        // POST /api/v1/products/bulk — publish / unpublish / archive / change-category / adjust-price
        group.MapPost("bulk", async Task<Results<Ok<BulkProductsResultDto>, BadRequest<ProblemDetails>>> (
            [FromBody] BulkProductsRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new BulkProductsCommand(request));
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(Problem(result));
        })
        .WithName("BulkProducts")
        .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        // POST /api/v1/products/bulk-delete
        group.MapPost("bulk-delete", async Task<Results<Ok<BulkProductsResultDto>, BadRequest<ProblemDetails>>> (
            [FromBody] BulkDeleteProductsRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new BulkDeleteProductsCommand(request));
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(Problem(result));
        })
        .WithName("BulkDeleteProducts")
        .RequirePermission(PermissionConstants.Catalog.Products.Delete);

        // POST /api/v1/products/bulk-duplicate
        group.MapPost("bulk-duplicate", async Task<Results<Ok<BulkProductsResultDto>, BadRequest<ProblemDetails>>> (
            [FromBody] BulkDuplicateProductsRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new BulkDuplicateProductsCommand(request));
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(Problem(result));
        })
        .WithName("BulkDuplicateProducts")
        .RequirePermission(PermissionConstants.Catalog.Products.Create);

        // POST /api/v1/products/bulk-export
        group.MapPost("bulk-export", async (
            [FromBody] ExportProductsRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new ExportProductsQuery(request));
            if (result.IsSuccess)
            {
                return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
            }

            return Results.BadRequest(Problem(result));
        })
        .WithName("ExportProducts")
        .RequirePermission(PermissionConstants.Catalog.Products.View);
    }

    private static async Task<Results<NoContent, BadRequest<ProblemDetails>>> SendEmptyResult(ISender sender, IRequest<Result> request)
    {
        var result = await sender.Send(request);
        return result.IsSuccess ? TypedResults.NoContent() : TypedResults.BadRequest(Problem(result));
    }

    private static ProblemDetails Problem(Result result) => new()
    {
        Title = "Bad Request",
        Detail = result.Error.Description,
        Type = result.Error.Code,
        Status = StatusCodes.Status400BadRequest
    };

    /// <summary>
    /// Parses the storefront's <c>attributes</c> query param — a JSON object mapping option-group
    /// key to selected values, e.g. <c>{"platform":["pc","xbox"]}</c> — into a filter dictionary.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? ParseAttributeFilters(string? attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(attributes)
                ?.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<string>)kvp.Value);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal sealed record DuplicateProductRequest(string? NameSuffix);

internal sealed record CreateProductRequest(string NameAr, string NameEn, string DescriptionAr, string DescriptionEn, decimal Price, Guid CategoryId, IReadOnlyList<Guid>? AdditionalCategoryIds = null, IReadOnlyList<Guid>? CollectionIds = null, DateTime? PublicReleaseOnUtc = null);
internal sealed record UpdateProductRequest(string NameAr, string NameEn, string DescriptionAr, string DescriptionEn, decimal Price, Guid CategoryId, ProductStatus Status, IReadOnlyList<Guid>? AdditionalCategoryIds = null, IReadOnlyList<Guid>? CollectionIds = null, DateTime? PublicReleaseOnUtc = null);
internal sealed record ChangeProductCategoryRequest(Guid CategoryId);
internal sealed record AdjustProductPriceRequest(PriceAdjustmentMode Mode, decimal Value);
