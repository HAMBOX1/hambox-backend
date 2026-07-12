using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.AssignAdminOrderManualCode;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.BulkAdminOrders;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.GetAdminOrderById;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.GetAdminOrders;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.GetAdminOrderStatistics;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.RefundAdminOrder;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.ResendAdminOrderCodes;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.RetryAdminOrderFulfillment;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.RevealAdminOrderLicenseKey;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.UpdateAdminOrderStatus;
using HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.UpsertAdminOrderNote;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class OrderManagementEndpoints
{
    public static void MapOrderManagementEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/orders")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Orders")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] string? orderStatus,
            [FromQuery] string? deliveryStatus,
            [FromQuery] string? paymentStatus,
            [FromQuery] string? dateRange,
            [FromQuery] DateTimeOffset? dateFrom,
            [FromQuery] DateTimeOffset? dateTo,
            [FromQuery] decimal? minAmount,
            [FromQuery] decimal? maxAmount,
            [FromQuery] string? sort,
            [FromQuery] string? orderKind,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : pageSize;
            var result = await sender.Send(new GetAdminOrdersQuery(
                pageNumber,
                pageSize,
                searchTerm,
                orderStatus,
                deliveryStatus,
                paymentStatus,
                dateRange,
                dateFrom,
                dateTo,
                minAmount,
                maxAmount,
                sort,
                orderKind));
            return MapResult(result);
        })
        .WithName("GetAdminOrders")
        .RequirePermission(PermissionConstants.Orders.View);

        group.MapGet("statistics", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetAdminOrderStatisticsQuery())))
        .WithName("GetAdminOrderStatistics")
        .RequirePermission(PermissionConstants.Orders.View);

        group.MapGet("{id:guid}/manage", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetAdminOrderByIdQuery(id))))
        .WithName("GetAdminOrderById")
        .RequirePermission(PermissionConstants.Orders.View);

        group.MapPatch("{id:guid}/status", async Task<IResult> (
            Guid id,
            [FromBody] UpdateAdminOrderStatusRequest request,
            ISender sender) =>
            MapResult(await sender.Send(new UpdateAdminOrderStatusCommand(id, request))))
        .WithName("UpdateAdminOrderStatus")
        .RequirePermission(PermissionConstants.Orders.Edit);

        group.MapPost("{id:guid}/refund", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new RefundAdminOrderCommand(id))))
        .WithName("RefundAdminOrder")
        .RequirePermission(PermissionConstants.Orders.Refund);

        group.MapPost("{id:guid}/resend-codes", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new ResendAdminOrderCodesCommand(id))))
        .WithName("ResendAdminOrderCodes")
        .RequirePermission(PermissionConstants.Orders.Edit);

        group.MapPost("{id:guid}/notes", async Task<IResult> (
            Guid id,
            [FromBody] UpsertAdminOrderNoteRequest request,
            ISender sender) =>
            MapResult(await sender.Send(new UpsertAdminOrderNoteCommand(id, null, request)), StatusCodes.Status201Created))
        .WithName("CreateAdminOrderNote")
        .RequirePermission(PermissionConstants.Orders.Edit);

        group.MapPut("{id:guid}/notes/{noteId:guid}", async Task<IResult> (
            Guid id,
            Guid noteId,
            [FromBody] UpsertAdminOrderNoteRequest request,
            ISender sender) =>
            MapResult(await sender.Send(new UpsertAdminOrderNoteCommand(id, noteId, request))))
        .WithName("UpdateAdminOrderNote")
        .RequirePermission(PermissionConstants.Orders.Edit);

        group.MapGet("{id:guid}/license-keys/{licenseKeyId:guid}/reveal", async Task<IResult> (
            Guid id,
            Guid licenseKeyId,
            ISender sender) =>
            MapResult(await sender.Send(new RevealAdminOrderLicenseKeyQuery(id, licenseKeyId))))
        .WithName("RevealAdminOrderLicenseKey")
        .RequirePermission(PermissionConstants.Orders.View);

        group.MapPost("bulk", async Task<IResult> (
            [FromBody] BulkAdminOrdersRequest request,
            ISender sender) =>
            MapResult(await sender.Send(new BulkAdminOrdersCommand(request))))
        .WithName("BulkAdminOrders")
        .RequirePermission(PermissionConstants.Orders.Edit);

        group.MapPost("{id:guid}/fulfillment/retry", async Task<IResult> (
            Guid id,
            ISender sender) =>
            MapResult(await sender.Send(new RetryAdminOrderFulfillmentCommand(id))))
        .WithName("RetryAdminOrderFulfillment")
        .RequirePermission(PermissionConstants.Orders.Edit);

        group.MapPost("{id:guid}/manual-code", async Task<IResult> (
            Guid id,
            [FromBody] AssignAdminOrderManualCodeRequest request,
            ISender sender) =>
            MapResult(await sender.Send(new AssignAdminOrderManualCodeCommand(id, request))))
        .WithName("AssignAdminOrderManualCode")
        .RequirePermission(PermissionConstants.Orders.Edit);
    }

    private static IResult MapResult(Result result, int successStatus = StatusCodes.Status200OK) =>
        result.IsSuccess
            ? Results.StatusCode(successStatus)
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });

    private static IResult MapResult<T>(Result<T> result, int successStatus = StatusCodes.Status200OK) =>
        result.IsSuccess
            ? successStatus == StatusCodes.Status200OK
                ? Results.Ok(result.Value)
                : Results.Created(string.Empty, result.Value)
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });
}
