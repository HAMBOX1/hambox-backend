using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Application.Features.Filters;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Catalog.Presentation.Endpoints;

/// <summary>The admin's filter library (custom filters, their values, and tagging products with them).</summary>
internal static class FilterDefinitionEndpoints
{
    public static void MapFilterDefinitionEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/filters")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Filters")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("", async Task<IResult> (ISender sender) => Map(await sender.Send(new GetFilterDefinitionsQuery())))
            .RequirePermission(PermissionConstants.Catalog.Categories.View);

        group.MapPost("", async Task<IResult> ([FromBody] SaveFilterDefinitionInput input, ISender sender) =>
                Map(await sender.Send(new SaveFilterDefinitionCommand(null, input))))
            .RequirePermission(PermissionConstants.Catalog.Categories.Edit);

        group.MapPut("{id:guid}", async Task<IResult> (Guid id, [FromBody] SaveFilterDefinitionInput input, ISender sender) =>
                Map(await sender.Send(new SaveFilterDefinitionCommand(id, input))))
            .RequirePermission(PermissionConstants.Catalog.Categories.Edit);

        group.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
                Map(await sender.Send(new DeleteFilterDefinitionCommand(id))))
            .RequirePermission(PermissionConstants.Catalog.Categories.Edit);

        // Tag many products with values of one filter (Merge / Replace / Remove).
        group.MapPost("assign", async Task<IResult> ([FromBody] AssignFilterValuesRequest body, ISender sender) =>
                Map(await sender.Send(new AssignFilterValuesCommand(body.ProductIds, body.FilterKey, body.Values, body.Mode))))
            .RequirePermission(PermissionConstants.Catalog.Products.Edit);

        group.MapGet("product/{productId:guid}", async Task<IResult> (Guid productId, ISender sender) =>
                Map(await sender.Send(new GetProductFilterValuesQuery(productId))))
            .RequirePermission(PermissionConstants.Catalog.Products.View);

        group.MapPut("product/{productId:guid}", async Task<IResult> (
                Guid productId, [FromBody] Dictionary<string, List<string>> values, ISender sender) =>
                Map(await sender.Send(new SetProductFilterValuesCommand(
                    productId, values.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value)))))
            .RequirePermission(PermissionConstants.Catalog.Products.Edit);
    }

    private static IResult Map(Result result) =>
        result.IsSuccess
            ? TypedResults.Ok()
            : TypedResults.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error.Description, Type = result.Error.Code });

    private static IResult Map<T>(Result<T> result) =>
        result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error.Description, Type = result.Error.Code });
}

internal sealed record AssignFilterValuesRequest(
    IReadOnlyList<Guid> ProductIds, string FilterKey, IReadOnlyList<string> Values, FilterAssignMode Mode);
