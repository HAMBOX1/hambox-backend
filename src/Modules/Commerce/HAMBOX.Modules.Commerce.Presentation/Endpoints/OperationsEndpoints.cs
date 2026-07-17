using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Operations;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class OperationsEndpoints
{
    public static void MapOperationsEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/operations")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Operations")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("dashboard", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetOperationsDashboardQuery())))
            .WithName("GetOperationsDashboard")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapGet("jobs", async Task<IResult> (
            [FromQuery] string? status,
            [FromQuery] string? jobType,
            [FromQuery] string? search,
            [FromQuery] string? queue,
            [FromQuery] DateTimeOffset? dateFrom,
            [FromQuery] DateTimeOffset? dateTo,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? sort,
            ISender sender) =>
            MapResult(await sender.Send(new GetOperationalJobsQuery(
                status, jobType, search, queue, dateFrom, dateTo, page, pageSize, sort))))
            .WithName("GetOperationalJobs")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapGet("jobs/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetOperationalJobByIdQuery(id))))
            .WithName("GetOperationalJobById")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapGet("jobs/{id:guid}/history", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetJobExecutionHistoryQuery(id))))
            .WithName("GetOperationalJobHistory")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapGet("recurring-jobs", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetRecurringJobDefinitionsQuery())))
            .WithName("GetRecurringJobDefinitions")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapPost("jobs/{id:guid}/retry", async Task<IResult> (Guid id, ISender sender) =>
            MapUnitResult(await sender.Send(new RetryOperationalJobCommand(id))))
            .WithName("RetryOperationalJob")
            .RequirePermission(PermissionConstants.Operations.Retry);

        group.MapPost("jobs/retry-all", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new RetryAllFailedJobsCommand())))
            .WithName("RetryAllFailedOperationalJobs")
            .RequirePermission(PermissionConstants.Operations.Retry);

        group.MapPost("jobs/{id:guid}/cancel", async Task<IResult> (Guid id, ISender sender) =>
            MapUnitResult(await sender.Send(new CancelOperationalJobCommand(id))))
            .WithName("CancelOperationalJob")
            .RequirePermission(PermissionConstants.Operations.Manage);

        group.MapPost("jobs/clear-completed", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new ClearCompletedJobsCommand())))
            .WithName("ClearCompletedOperationalJobs")
            .RequirePermission(PermissionConstants.Operations.Clear);

        group.MapGet("delivery", async Task<IResult> (
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            ISender sender) =>
            MapResult(await sender.Send(new GetDeliveryMonitorQuery(status, search, page, pageSize))))
            .WithName("GetDeliveryMonitor")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapPost("delivery/{orderId:guid}/retry", async Task<IResult> (Guid orderId, ISender sender) =>
            MapResult(await sender.Send(new RetryDeliveryCommand(orderId))))
            .WithName("RetryDelivery")
            .RequirePermission(PermissionConstants.Operations.Retry);

        group.MapGet("suppliers", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetSupplierOperationsQuery())))
            .WithName("GetSupplierOperations")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapGet("api-logs", async Task<IResult> (
            [FromQuery] string? method,
            [FromQuery] string? path,
            [FromQuery] string? userId,
            [FromQuery] int? minStatusCode,
            [FromQuery] int? maxStatusCode,
            [FromQuery] DateTimeOffset? dateFrom,
            [FromQuery] DateTimeOffset? dateTo,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            ISender sender) =>
            MapResult(await sender.Send(new GetApiRequestLogsQuery(
                method, path, userId, minStatusCode, maxStatusCode, dateFrom, dateTo, page, pageSize))))
            .WithName("GetApiRequestLogs")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapGet("health", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetSystemHealthQuery())))
            .WithName("GetSystemHealth")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapGet("alerts", async Task<IResult> (
            [FromQuery] bool? acknowledged,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            ISender sender) =>
            MapResult(await sender.Send(new GetOperationalAlertsQuery(acknowledged, page, pageSize))))
            .WithName("GetOperationalAlerts")
            .RequirePermission(PermissionConstants.Operations.View);

        group.MapPost("alerts/{id:guid}/acknowledge", async Task<IResult> (Guid id, ISender sender) =>
            MapUnitResult(await sender.Send(new AcknowledgeOperationalAlertCommand(id))))
            .WithName("AcknowledgeOperationalAlert")
            .RequirePermission(PermissionConstants.Operations.Manage);

        group.MapGet("export", async Task<IResult> (
            [FromQuery] string type,
            [FromQuery] string format,
            ISender sender) =>
        {
            var result = await sender.Send(new ExportOperationsQuery(type, format));
            if (!result.IsSuccess)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Bad Request",
                    Detail = result.Error.Description,
                    Type = result.Error.Code,
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            var export = result.Value;
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(export.Content),
                export.ContentType,
                export.FileName);
        })
            .WithName("ExportOperations")
            .RequirePermission(PermissionConstants.Operations.Export);
    }

    private static IResult MapResult<T>(Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });

    private static IResult MapUnitResult(Result result) =>
        result.IsSuccess
            ? Results.Ok()
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });
}
