using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Storefront.GetStorefrontContent;
using HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration;
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
/// Registers public storefront endpoints.
/// </summary>
internal static class StorefrontEndpoints
{
    public static void MapStorefrontEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/storefront")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Storefront")
            .HasApiVersion(1);

        group.MapGet("content", async Task<Results<Ok<StorefrontContentDto>, BadRequest<ProblemDetails>>> (ISender sender) =>
        {
            var result = await sender.Send(new GetStorefrontContentQuery());

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
        .WithName("GetStorefrontContent")
        .AllowAnonymous();

        group.MapGet("products/{productId:guid}/configuration", async Task<Results<Ok<StorefrontProductConfigurationDto>, BadRequest<ProblemDetails>>> (
            Guid productId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetStorefrontProductConfigurationQuery(productId));

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
        .WithName("GetStorefrontProductConfiguration")
        .AllowAnonymous();

        // Bulk form for product listing pages — one request for the whole page of cards instead
        // of one per card. Ids that don't resolve to an active product are silently omitted.
        group.MapGet("products/configuration", async Task<Ok<IReadOnlyList<StorefrontProductConfigurationDto>>> (
            Guid[] ids,
            ISender sender) =>
        {
            var result = await sender.Send(new GetStorefrontProductConfigurationsQuery(ids));
            return TypedResults.Ok(result.Value);
        })
        .WithName("GetStorefrontProductConfigurations")
        .AllowAnonymous();

        // GET /api/v1/storefront/products/{productId}/configuration/preview — admin-only, ignores Draft/Inactive status
        // so an owner can preview a product before publishing it.
        group.MapGet("products/{productId:guid}/configuration/preview", async Task<Results<Ok<StorefrontProductConfigurationDto>, BadRequest<ProblemDetails>>> (
            Guid productId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetStorefrontProductConfigurationQuery(productId, AllowUnpublished: true));

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
        .WithName("GetStorefrontProductConfigurationPreview")
        .RequirePermission(PermissionConstants.Catalog.Products.View);
    }
}
