using Asp.Versioning.Builder;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
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

internal static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/inventory")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Inventory")
            .HasApiVersion(1);

        group.MapGet("/statistics", async (
            [FromQuery] Guid? productId,
            [FromQuery] Guid? variantId,
            ISender sender) => await Send(sender, new GetInventoryStatisticsQuery(productId, variantId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

        group.MapGet("/products/{productId:guid}/variants", async (Guid productId, ISender sender) =>
            await Send(sender, new GetProductVariantsQuery(productId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

        group.MapPost("/products/{productId:guid}/variants", async (
            Guid productId,
            [FromBody] CreateVariantRequest body,
            ISender sender) => await SendCreated(sender, new CreateProductVariantCommand(
                productId, body.Sku, body.PlanId, body.PriceOverride, body.ComparePrice,
                body.SortOrder, body.MembershipPlanId, body.LowStockThreshold, body.OptionIds)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Create);

        group.MapPost("/products/{productId:guid}/variants/generate", async (Guid productId, ISender sender) =>
            await Send(sender, new GenerateProductVariantsCommand(productId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Create);

        group.MapPost("/products/{productId:guid}/variants/bulk-update", async (
            Guid productId,
            [FromBody] BulkUpdateVariantsRequest body,
            ISender sender) => await Send(sender, new BulkUpdateProductVariantsCommand(
                productId, body.VariantIds, body.PriceOverride, body.Status)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapPost("/products/{productId:guid}/variants/activate", async (Guid productId, ISender sender) =>
            await Send(sender, new ActivateProductVariantsCommand(productId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapPut("/variants/{variantId:guid}", async (
            Guid variantId,
            [FromBody] UpdateVariantRequest body,
            ISender sender) => await SendEmpty(sender, new UpdateProductVariantCommand(
                variantId, body.Sku, body.PlanId, body.PriceOverride, body.ComparePrice,
                body.SortOrder, body.Status, body.IsVisible, body.MembershipPlanId,
                body.LowStockThreshold, body.OptionIds)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapGet("/variants/{variantId:guid}/usage", async (Guid variantId, ISender sender) =>
            await Send(sender, new GetProductVariantUsageQuery(variantId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

        group.MapPost("/variants/{variantId:guid}/cleanup", async (Guid variantId, ISender sender) =>
            await Send(sender, new CleanupProductVariantCommand(variantId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Delete);

        group.MapDelete("/variants/{variantId:guid}", async (Guid variantId, ISender sender) =>
            await SendEmpty(sender, new DeleteProductVariantCommand(variantId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Delete);

        group.MapPost("/variants/{variantId:guid}/duplicate", async (
            Guid variantId,
            [FromBody] DuplicateVariantRequest? body,
            ISender sender) => await SendCreated(sender, new DuplicateProductVariantCommand(variantId, body?.SkuSuffix)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Create);

        group.MapPost("/variants/{variantId:guid}/activate", async (Guid variantId, ISender sender) =>
            await SendEmpty(sender, new ActivateProductVariantCommand(variantId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapPost("/variants/{variantId:guid}/deactivate", async (Guid variantId, ISender sender) =>
            await SendEmpty(sender, new DeactivateProductVariantCommand(variantId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapPost("/variants/{variantId:guid}/archive", async (Guid variantId, ISender sender) =>
            await SendEmpty(sender, new ArchiveProductVariantCommand(variantId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapGet("/products/{productId:guid}/option-groups", async (Guid productId, ISender sender) =>
            await Send(sender, new GetProductOptionGroupsQuery(productId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

        group.MapPost("/products/{productId:guid}/option-groups", async (
            Guid productId,
            [FromBody] CreateOptionGroupRequest body,
            ISender sender) => await SendCreated(sender, new CreateProductOptionGroupCommand(
                productId, body.Key, body.DisplayName, body.SortOrder, body.IsRequired, body.ParentOptionId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Create);

        group.MapPost("/option-groups/{groupId:guid}/options", async (
            Guid groupId,
            [FromBody] CreateOptionRequest body,
            ISender sender) => await SendCreated(sender, new CreateProductOptionCommand(groupId, body.Value, body.Label, body.SortOrder, body.DescriptionHtml)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Create);

        group.MapGet("/option-group-templates", async (
            [FromQuery] string? search,
            ISender sender) => await Send(sender, new SearchOptionGroupTemplatesQuery(search)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

        group.MapGet("/option-group-templates/{templateId:guid}", async (Guid templateId, ISender sender) =>
            await Send(sender, new GetOptionGroupTemplateQuery(templateId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

        group.MapPost("/option-groups/{groupId:guid}/save-as-template", async (
            Guid groupId,
            [FromBody] SaveOptionGroupAsTemplateRequest body,
            ISender sender) => await SendCreated(sender, new SaveOptionGroupAsTemplateCommand(groupId, body.Name)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Create);

        group.MapPut("/option-group-templates/{templateId:guid}", async (
            Guid templateId,
            [FromBody] UpdateOptionGroupTemplateRequest body,
            ISender sender) => await SendEmpty(sender, new UpdateOptionGroupTemplateCommand(templateId, body.Name, body.IsRequiredDefault, body.Options)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapDelete("/option-group-templates/{templateId:guid}", async (Guid templateId, ISender sender) =>
            await SendEmpty(sender, new DeleteOptionGroupTemplateCommand(templateId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Delete);

        group.MapPost("/products/{productId:guid}/option-groups/import-template", async (
            Guid productId,
            [FromBody] ImportOptionGroupTemplateRequest body,
            ISender sender) => await SendCreated(sender, new ImportOptionGroupTemplateCommand(productId, body.TemplateId, body.Resolution)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Create);

        group.MapPut("/option-groups/{groupId:guid}", async (
            Guid groupId,
            [FromBody] UpdateOptionGroupRequest body,
            ISender sender) => await SendEmpty(sender, new UpdateProductOptionGroupCommand(groupId, body.DisplayName, body.SortOrder, body.IsRequired)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapDelete("/option-groups/{groupId:guid}", async (Guid groupId, [FromQuery] bool force, ISender sender) =>
            await SendEmpty(sender, new DeleteProductOptionGroupCommand(groupId, force)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Delete);

        group.MapPut("/products/{productId:guid}/option-groups/reorder", async (
            Guid productId,
            [FromBody] ReorderIdsRequest body,
            ISender sender) => await SendEmpty(sender, new ReorderProductOptionGroupsCommand(productId, body.OrderedIds)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapPut("/options/{optionId:guid}", async (
            Guid optionId,
            [FromBody] UpdateOptionRequest body,
            ISender sender) => await SendEmpty(sender, new UpdateProductOptionCommand(optionId, body.Label, body.SortOrder, body.DescriptionHtml)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapDelete("/options/{optionId:guid}", async (Guid optionId, ISender sender) =>
            await SendEmpty(sender, new DeleteProductOptionCommand(optionId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Delete);

        group.MapPut("/option-groups/{groupId:guid}/options/reorder", async (
            Guid groupId,
            [FromBody] ReorderIdsRequest body,
            ISender sender) => await SendEmpty(sender, new ReorderProductOptionsCommand(groupId, body.OrderedIds)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapGet("/suppliers", async (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            ISender sender) => await Send(sender, new GetSuppliersQuery(
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 20 : pageSize,
                searchTerm)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.ManageSuppliers);

        group.MapPost("/suppliers", async ([FromBody] CreateSupplierRequest body, ISender sender) =>
            await SendCreated(sender, new CreateSupplierCommand(
                body.CompanyName, body.ContactPerson, body.Email, body.Phone,
                body.Website, body.Country, body.Currency, body.Notes)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.ManageSuppliers);

        group.MapGet("/variants/{variantId:guid}/batches", async (Guid variantId, ISender sender) =>
            await Send(sender, new GetInventoryBatchesQuery(variantId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.ManageBatches);

        group.MapPost("/variants/{variantId:guid}/batches", async (
            Guid variantId,
            [FromBody] CreateBatchRequest body,
            ISender sender) => await SendCreated(sender, new CreateInventoryBatchCommand(
                variantId, body.Name, body.SupplierId, body.PurchaseDate,
                body.Currency, body.PurchaseCost, body.ExpectedMargin, body.Notes)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.ManageBatches);

        group.MapPost("/variants/{variantId:guid}/batches/{batchId:guid}/import", async (
            Guid variantId,
            Guid batchId,
            [FromBody] ImportCodesRequest body,
            ISender sender) => await Send(sender, new ImportInventoryCodesCommand(variantId, batchId, body.Codes)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Import);

        group.MapGet("/variants/{variantId:guid}/codes", async (
            Guid variantId,
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? status,
            [FromQuery] string? searchTerm,
            ISender sender) => await Send(sender, new GetInventoryCodesQuery(
                variantId,
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 50 : pageSize,
                status,
                searchTerm)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.ManageCodes);

        group.MapGet("/audit-logs", async (
            [FromQuery] Guid? variantId,
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            ISender sender) => await Send(sender, new GetInventoryAuditLogsQuery(
                variantId,
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 50 : pageSize)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

        group.MapGet("/reservations", async (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            ISender sender) => await Send(sender, new GetActiveReservationsQuery(
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 50 : pageSize)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.View);

        group.MapPost("/reservations/{codeId:guid}/release", async (Guid codeId, ISender sender) =>
            await SendEmpty(sender, new ReleaseInventoryReservationCommand(codeId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapPost("/codes/{codeId:guid}/disable", async (Guid codeId, ISender sender) =>
            await SendEmpty(sender, new DisableInventoryCodeCommand(codeId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapPost("/codes/{codeId:guid}/enable", async (Guid codeId, ISender sender) =>
            await SendEmpty(sender, new EnableInventoryCodeCommand(codeId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapDelete("/codes/{codeId:guid}", async (Guid codeId, ISender sender) =>
            await SendEmpty(sender, new DeleteInventoryCodeCommand(codeId)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Delete);

        group.MapPost("/codes/{codeId:guid}/reveal", async (Guid codeId, HttpContext httpContext, ISender sender) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";
            return await Send(sender, new RevealInventoryCodeCommand(codeId, ipAddress, userAgent));
        })
        .RequirePermission(PermissionConstants.Catalog.Inventory.RevealCodes);

        group.MapPost("/variants/{variantId:guid}/codes/bulk-disable", async (
            Guid variantId,
            [FromBody] BulkCodeIdsRequest body,
            ISender sender) => await Send(sender, new BulkDisableInventoryCodesCommand(variantId, body.CodeIds)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Edit);

        group.MapPost("/variants/{variantId:guid}/codes/bulk-delete", async (
            Guid variantId,
            [FromBody] BulkCodeIdsRequest body,
            ISender sender) => await Send(sender, new BulkDeleteInventoryCodesCommand(variantId, body.CodeIds)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Delete);

        group.MapGet("/variants/{variantId:guid}/codes/export", async (
            Guid variantId,
            [FromQuery] string? status,
            ISender sender) =>
        {
            var result = await sender.Send(new ExportInventoryCodesQuery(variantId, status));
            if (result.IsSuccess)
            {
                return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
            }

            return Results.BadRequest(Problem(result));
        })
        .RequirePermission(PermissionConstants.Catalog.Inventory.Export);

        group.MapPost("/products/{productId:guid}/variants/bulk-delete", async (
            Guid productId,
            [FromBody] BulkVariantIdsRequest body,
            ISender sender) => await Send(sender, new BulkDeleteProductVariantsCommand(productId, body.VariantIds)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Delete);

        group.MapPost("/products/{productId:guid}/variants/bulk-duplicate", async (
            Guid productId,
            [FromBody] BulkVariantIdsRequest body,
            ISender sender) => await Send(sender, new BulkDuplicateProductVariantsCommand(productId, body.VariantIds)))
            .RequirePermission(PermissionConstants.Catalog.Inventory.Create);

        group.MapGet("/products/{productId:guid}/variants/codes/export", async (
            Guid productId,
            [FromQuery] Guid[] variantIds,
            [FromQuery] string? status,
            ISender sender) =>
        {
            var result = await sender.Send(new ExportVariantsInventoryCodesQuery(productId, variantIds, status));
            if (result.IsSuccess)
            {
                return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
            }

            return Results.BadRequest(Problem(result));
        })
        .RequirePermission(PermissionConstants.Catalog.Inventory.Export);
    }

    private static async Task<Results<Ok<T>, BadRequest<ProblemDetails>>> Send<T>(ISender sender, IRequest<Result<T>> request)
    {
        var result = await sender.Send(request);
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return TypedResults.BadRequest(Problem(result));
    }

    private static async Task<Results<Ok<Guid>, BadRequest<ProblemDetails>>> SendCreated(ISender sender, IRequest<Result<Guid>> request)
    {
        var result = await sender.Send(request);
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return TypedResults.BadRequest(Problem(result));
    }

    private static async Task<Results<NoContent, BadRequest<ProblemDetails>>> SendEmpty(ISender sender, IRequest<Result> request)
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
}

internal sealed record CreateVariantRequest(
    string Sku,
    Guid? PlanId,
    decimal? PriceOverride,
    decimal? ComparePrice,
    int SortOrder,
    Guid? MembershipPlanId,
    int LowStockThreshold,
    IReadOnlyList<Guid> OptionIds);

internal sealed record CreateOptionGroupRequest(string Key, string DisplayName, int SortOrder, bool IsRequired, Guid? ParentOptionId = null);
internal sealed record CreateOptionRequest(string Value, string Label, int SortOrder, string? DescriptionHtml = null);
internal sealed record SaveOptionGroupAsTemplateRequest(string Name);
internal sealed record UpdateOptionGroupTemplateRequest(string Name, bool IsRequiredDefault, IReadOnlyList<OptionGroupTemplateOptionInput> Options);
internal sealed record ImportOptionGroupTemplateRequest(Guid TemplateId, ImportConflictResolution Resolution);
internal sealed record CreateSupplierRequest(
    string CompanyName,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Website,
    string? Country,
    string Currency,
    string? Notes);
internal sealed record CreateBatchRequest(
    string Name,
    Guid? SupplierId,
    DateTimeOffset? PurchaseDate,
    string Currency,
    decimal PurchaseCost,
    decimal? ExpectedMargin,
    string? Notes);
internal sealed record ImportCodesRequest(IReadOnlyList<string> Codes);

internal sealed record UpdateVariantRequest(
    string Sku,
    Guid? PlanId,
    decimal? PriceOverride,
    decimal? ComparePrice,
    int SortOrder,
    ProductVariantStatus Status,
    bool IsVisible,
    Guid? MembershipPlanId,
    int LowStockThreshold,
    IReadOnlyList<Guid> OptionIds);

internal sealed record DuplicateVariantRequest(string? SkuSuffix);
internal sealed record BulkUpdateVariantsRequest(
    IReadOnlyList<Guid> VariantIds,
    decimal? PriceOverride,
    ProductVariantStatus? Status);
internal sealed record UpdateOptionGroupRequest(string DisplayName, int SortOrder, bool IsRequired);
internal sealed record UpdateOptionRequest(string Label, int SortOrder, string? DescriptionHtml = null);
internal sealed record ReorderIdsRequest(IReadOnlyList<Guid> OrderedIds);
internal sealed record BulkCodeIdsRequest(IReadOnlyList<Guid> CodeIds);
internal sealed record BulkVariantIdsRequest(IReadOnlyList<Guid> VariantIds);
