using Asp.Versioning.Builder;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Suppliers.Presentation.Endpoints;

internal static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/suppliers")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Suppliers")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("", async Task<IResult> (
                [FromQuery] string? search, [FromQuery] string? status,
                [FromQuery] int page, [FromQuery] int pageSize, ISender sender) =>
            MapResult(await sender.Send(new GetSuppliersQuery(search, status, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize))))
            .RequirePermission(PermissionConstants.Suppliers.View);

        group.MapGet("provider-types", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetSupplierProviderTypesQuery())))
            .RequirePermission(PermissionConstants.Suppliers.View);

        group.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetSupplierByIdQuery(id))))
            .RequirePermission(PermissionConstants.Suppliers.View);

        group.MapPost("", async Task<IResult> ([FromBody] CreateSupplierRequest request, ISender sender) =>
            MapResult(await sender.Send(new CreateSupplierCommand(request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Suppliers.Create);

        group.MapPut("{id:guid}", async Task<IResult> (Guid id, [FromBody] UpdateSupplierRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateSupplierCommand(id, request))))
            .RequirePermission(PermissionConstants.Suppliers.Edit);

        group.MapPut("{id:guid}/credentials", async Task<IResult> (Guid id, [FromBody] UpdateSupplierCredentialsRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateSupplierCredentialsCommand(id, request))))
            .RequirePermission(PermissionConstants.Suppliers.Edit);

        group.MapPut("{id:guid}/settings", async Task<IResult> (Guid id, [FromBody] UpdateSupplierSettingsRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateSupplierSettingsCommand(id, request))))
            .RequirePermission(PermissionConstants.Suppliers.Edit);

        group.MapPut("{id:guid}/priority", async Task<IResult> (Guid id, [FromBody] UpdateSupplierPriorityRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateSupplierPriorityCommand(id, request.Priority))))
            .RequirePermission(PermissionConstants.Suppliers.Edit);

        group.MapPost("{id:guid}/enable", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new EnableSupplierCommand(id))))
            .RequirePermission(PermissionConstants.Suppliers.Edit);

        group.MapPost("{id:guid}/disable", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DisableSupplierCommand(id))))
            .RequirePermission(PermissionConstants.Suppliers.Edit);

        group.MapPost("{id:guid}/test-connection", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new TestSupplierConnectionCommand(id))))
            .RequirePermission(PermissionConstants.Suppliers.Edit);

        group.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteSupplierCommand(id))))
            .RequirePermission(PermissionConstants.Suppliers.Delete);

        var mappings = app.MapGroup("api/v{version:apiVersion}/suppliers/{supplierId:guid}/mappings")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Suppliers")
            .HasApiVersion(1)
            .RequireAuthorization();

        mappings.MapGet("", async Task<IResult> (Guid supplierId, ISender sender) =>
            MapResult(await sender.Send(new GetSupplierMappingsQuery(supplierId))))
            .RequirePermission(PermissionConstants.Suppliers.View);

        mappings.MapPost("", async Task<IResult> (Guid supplierId, [FromBody] CreateSupplierMappingRequest request, ISender sender) =>
            MapResult(await sender.Send(new CreateSupplierMappingCommand(supplierId, request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);

        mappings.MapPut("{mappingId:guid}", async Task<IResult> (Guid supplierId, Guid mappingId, [FromBody] UpdateSupplierMappingRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateSupplierMappingCommand(supplierId, mappingId, request))))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);

        mappings.MapDelete("{mappingId:guid}", async Task<IResult> (Guid supplierId, Guid mappingId, ISender sender) =>
            MapResult(await sender.Send(new DeleteSupplierMappingCommand(supplierId, mappingId))))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}
