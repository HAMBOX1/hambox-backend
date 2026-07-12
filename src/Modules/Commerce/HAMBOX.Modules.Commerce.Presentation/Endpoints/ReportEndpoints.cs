using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Reports;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/reports")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Reports")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("types", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetReportTypesQuery())))
            .WithName("GetReportTypes")
            .RequirePermission(PermissionConstants.Reports.View);

        group.MapGet("library", async Task<IResult> (
            [FromQuery] string? search,
            [FromQuery] string? reportType,
            ISender sender) =>
            MapResult(await sender.Send(new GetReportLibraryQuery(search, reportType))))
            .WithName("GetReportLibrary")
            .RequirePermission(PermissionConstants.Reports.View);

        group.MapPost("definitions", async Task<IResult> (
            [FromBody] CreateReportDefinitionRequest body,
            ISender sender) =>
            MapResult(await sender.Send(new CreateReportDefinitionCommand(
                body.Name,
                body.ReportType,
                body.FiltersJson,
                body.FormatDefault ?? "pdf"))))
            .WithName("CreateReportDefinition")
            .RequirePermission(PermissionConstants.Reports.View);

        group.MapPut("definitions/{id:guid}", async Task<IResult> (
            Guid id,
            [FromBody] UpdateReportDefinitionRequest body,
            ISender sender) =>
            MapResult(await sender.Send(new UpdateReportDefinitionCommand(
                id,
                body.Name,
                body.FiltersJson,
                body.FormatDefault ?? "pdf"))))
            .WithName("UpdateReportDefinition")
            .RequirePermission(PermissionConstants.Reports.View);

        group.MapDelete("definitions/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapUnitResult(await sender.Send(new DeleteReportDefinitionCommand(id))))
            .WithName("DeleteReportDefinition")
            .RequirePermission(PermissionConstants.Reports.Delete);

        group.MapPost("favorites/{definitionId:guid}", async Task<IResult> (Guid definitionId, ISender sender) =>
            MapUnitResult(await sender.Send(new AddReportFavoriteCommand(definitionId))))
            .WithName("AddReportFavorite")
            .RequirePermission(PermissionConstants.Reports.View);

        group.MapDelete("favorites/{definitionId:guid}", async Task<IResult> (Guid definitionId, ISender sender) =>
            MapUnitResult(await sender.Send(new RemoveReportFavoriteCommand(definitionId))))
            .WithName("RemoveReportFavorite")
            .RequirePermission(PermissionConstants.Reports.View);

        group.MapGet("favorites", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetReportFavoritesQuery())))
            .WithName("GetReportFavorites")
            .RequirePermission(PermissionConstants.Reports.View);

        group.MapGet("downloads", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetReportDownloadsQuery())))
            .WithName("GetReportDownloads")
            .RequirePermission(PermissionConstants.Reports.View);

        group.MapPost("generate", async Task<IResult> (
            [FromBody] GenerateReportRequest body,
            ISender sender) =>
        {
            var result = await sender.Send(new GenerateReportCommand(
                body.ReportType,
                body.Format,
                body.Preset,
                body.From,
                body.To,
                body.Status,
                body.CategoryId,
                body.MembershipPlanId,
                body.PromotionId,
                body.Country,
                body.Currency));

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

            var file = result.Value;
            return Results.File(file.Content, file.ContentType, file.FileName);
        })
            .WithName("GenerateReport")
            .RequirePermission(PermissionConstants.Reports.Export);

        group.MapGet("schedules", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetScheduledReportsQuery())))
            .WithName("GetScheduledReports")
            .RequirePermission(PermissionConstants.Reports.View);

        group.MapPost("schedules", async Task<IResult> (
            [FromBody] CreateScheduledReportRequest body,
            ISender sender) =>
            MapResult(await sender.Send(new CreateScheduledReportCommand(
                body.ReportType,
                body.FiltersJson,
                body.Format ?? "pdf",
                body.Frequency ?? "Daily",
                body.EmailRecipients ?? [],
                body.IsEnabled,
                body.ReportDefinitionId))))
            .WithName("CreateScheduledReport")
            .RequirePermission(PermissionConstants.Reports.Schedule);

        group.MapPut("schedules/{id:guid}", async Task<IResult> (
            Guid id,
            [FromBody] UpdateScheduledReportRequest body,
            ISender sender) =>
            MapResult(await sender.Send(new UpdateScheduledReportCommand(
                id,
                body.FiltersJson,
                body.Format ?? "pdf",
                body.Frequency ?? "Daily",
                body.EmailRecipients ?? [],
                body.IsEnabled))))
            .WithName("UpdateScheduledReport")
            .RequirePermission(PermissionConstants.Reports.Schedule);

        group.MapDelete("schedules/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapUnitResult(await sender.Send(new DeleteScheduledReportCommand(id))))
            .WithName("DeleteScheduledReport")
            .RequirePermission(PermissionConstants.Reports.Schedule);

        group.MapPost("schedules/{id:guid}/run", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new RunScheduledReportCommand(id))))
            .WithName("RunScheduledReport")
            .RequirePermission(PermissionConstants.Reports.Schedule);

        group.MapGet("schedules/{id:guid}/executions", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetScheduledReportExecutionsQuery(id))))
            .WithName("GetScheduledReportExecutions")
            .RequirePermission(PermissionConstants.Reports.View);
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
            ? Results.NoContent()
            : Results.BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = result.Error.Description,
                Type = result.Error.Code,
                Status = StatusCodes.Status400BadRequest,
            });

    private sealed record CreateReportDefinitionRequest(
        string Name,
        string ReportType,
        string? FiltersJson,
        string? FormatDefault);

    private sealed record UpdateReportDefinitionRequest(
        string Name,
        string? FiltersJson,
        string? FormatDefault);

    private sealed record GenerateReportRequest(
        string ReportType,
        string Format,
        string? Preset,
        DateTimeOffset? From,
        DateTimeOffset? To,
        string? Status,
        Guid? CategoryId,
        Guid? MembershipPlanId,
        Guid? PromotionId,
        string? Country,
        string? Currency);

    private sealed record CreateScheduledReportRequest(
        string ReportType,
        string? FiltersJson,
        string? Format,
        string? Frequency,
        IReadOnlyList<string>? EmailRecipients,
        bool IsEnabled,
        Guid? ReportDefinitionId);

    private sealed record UpdateScheduledReportRequest(
        string? FiltersJson,
        string? Format,
        string? Frequency,
        IReadOnlyList<string>? EmailRecipients,
        bool IsEnabled);
}
