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

        // Gated on Catalog.Inventory.View (not Suppliers.View) deliberately — this is consumed from
        // the Catalog product/variant editor's Fulfillment section, returns only safe metadata already
        // classified as such (supplier name, provider type, enabled, credentials-configured boolean,
        // priority, mapping status), and a Catalog-permission admin should be able to see why a
        // variant's automated fulfillment is or isn't ready without needing separate Suppliers access.
        group.MapGet("fulfillment-chain", async Task<IResult> (
                [FromQuery] Guid productId, [FromQuery] Guid? variantId, ISender sender) =>
            MapResult(await sender.Send(new GetSupplierFulfillmentChainQuery(productId, variantId))))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

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

        // Read-only catalog browse for the product-mapping picker — same permission tier as viewing the
        // supplier itself (Suppliers.View), since it never mutates anything; the actual mapping write
        // still requires Suppliers.ManageMappings on the mappings endpoints below.
        group.MapGet("{id:guid}/catalog", async Task<IResult> (
                Guid id, [FromQuery] string? search, [FromQuery] int page, [FromQuery] int pageSize, ISender sender) =>
            MapResult(await sender.Send(new SearchSupplierCatalogQuery(id, search, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize))))
            .RequirePermission(PermissionConstants.Suppliers.View);

        // Safe, aggregate-only counts for the supplier detail page's availability status section —
        // never exposes a per-mapping external id or credential.
        group.MapGet("{id:guid}/availability-summary", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetSupplierAvailabilitySummaryQuery(id))))
            .RequirePermission(PermissionConstants.Suppliers.View);

        // Admin-triggered refresh — same ISupplierAvailabilityService the recurring background job
        // calls, just invoked synchronously for one supplier. Gated on ManageMappings (not just View)
        // since it's a write action (updates SupplierProductAvailability + writes an audit log row).
        group.MapPost("{id:guid}/availability/sync", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new SyncSupplierAvailabilityCommand(id))))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);

        group.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteSupplierCommand(id))))
            .RequirePermission(PermissionConstants.Suppliers.Delete);

        // Cross-supplier product mapping status — consumed by the Catalog product list's Supplier
        // Mapping column/filter, so gated on the Catalog permission rather than Suppliers.View, mirroring
        // the fulfillment-chain endpoint above.
        group.MapPost("product-mapping-status", async Task<IResult> (
                [FromBody] GetSupplierMappingStatusForProductsRequest request, ISender sender) =>
            MapResult(await sender.Send(new GetSupplierMappingStatusForProductsQuery(request.ProductIds))))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

        // Per-variant mapping breakdown for one product — the product-centric mapping drawer's and the
        // product edit page's Supplier Fulfillment card's shared data source.
        group.MapGet("product-mappings", async Task<IResult> ([FromQuery] Guid productId, ISender sender) =>
            MapResult(await sender.Send(new GetProductVariantSupplierMappingsQuery(productId))))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

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

        mappings.MapPut("{mappingId:guid}/priority", async Task<IResult> (
                Guid supplierId, Guid mappingId, [FromBody] UpdateMappingPriorityRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateSupplierMappingPriorityCommand(supplierId, mappingId, request.Priority))))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);

        mappings.MapDelete("{mappingId:guid}", async Task<IResult> (Guid supplierId, Guid mappingId, ISender sender) =>
            MapResult(await sender.Send(new DeleteSupplierMappingCommand(supplierId, mappingId))))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);

        // The Map Products workspace — same ManageMappings gate as the rest of this group and the
        // frontend route (:id/map-products) that hosts it.
        var mappingsRoot = app.MapGroup("api/v{version:apiVersion}/suppliers/{supplierId:guid}")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Suppliers")
            .HasApiVersion(1)
            .RequireAuthorization();

        mappingsRoot.MapGet("mapping-candidates", async Task<IResult> (
                Guid supplierId, [FromQuery] string? search, [FromQuery] string? status,
                [FromQuery] int page, [FromQuery] int pageSize, ISender sender) =>
            MapResult(await sender.Send(new GetSupplierMappingCandidatesQuery(
                supplierId, search, status, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize))))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);

        mappingsRoot.MapGet("mapping-candidates/summary", async Task<IResult> (Guid supplierId, ISender sender) =>
            MapResult(await sender.Send(new GetSupplierMappingCandidatesSummaryQuery(supplierId))))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);

        mappingsRoot.MapPost("mappings/suggest", async Task<IResult> (
                Guid supplierId, [FromBody] IReadOnlyList<SuggestionCandidate> candidates, ISender sender) =>
            MapResult(await sender.Send(new SuggestSupplierMappingsQuery(supplierId, candidates))))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);

        mappingsRoot.MapPost("mappings/bulk", async Task<IResult> (
                Guid supplierId, [FromBody] IReadOnlyList<CreateSupplierMappingRequest> requests, ISender sender) =>
            MapResult(await sender.Send(new BulkCreateSupplierMappingsCommand(supplierId, requests))))
            .RequirePermission(PermissionConstants.Suppliers.ManageMappings);
    }

    internal sealed record UpdateMappingPriorityRequest(int Priority);

    internal sealed record GetSupplierMappingStatusForProductsRequest(IReadOnlyList<Guid>? ProductIds);

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}
