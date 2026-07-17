using Asp.Versioning.Builder;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Contracts;
using HAMBOX.Modules.Communication.Application.Features.AuditLog;
using HAMBOX.Modules.Communication.Application.Features.Dashboard;
using HAMBOX.Modules.Communication.Application.Features.Messages;
using HAMBOX.Modules.Communication.Application.Features.Preferences;
using HAMBOX.Modules.Communication.Application.Features.Providers;
using HAMBOX.Modules.Communication.Application.Features.Templates;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Communication.Presentation.Endpoints;

internal static class CommunicationEndpoints
{
    public static void MapCommunicationAdminEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/communication")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Communication")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("dashboard/stats", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetCommunicationDashboardStatsQuery())))
            .RequirePermission(PermissionConstants.Communication.View);

        group.MapGet("templates", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetCommunicationTemplatesQuery())))
            .RequirePermission(PermissionConstants.Communication.View);

        group.MapGet("templates/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetCommunicationTemplateQuery(id))))
            .RequirePermission(PermissionConstants.Communication.View);

        group.MapPost("templates", async Task<IResult> ([FromBody] CreateCommunicationTemplateRequest request, ISender sender) =>
            MapResult(await sender.Send(new CreateCommunicationTemplateCommand(request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Communication.ManageTemplates);

        group.MapPut("templates/{id:guid}", async Task<IResult> (Guid id, [FromBody] UpdateCommunicationTemplateRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateCommunicationTemplateCommand(id, request))))
            .RequirePermission(PermissionConstants.Communication.ManageTemplates);

        group.MapPost("templates/{id:guid}/publish", async Task<IResult> (Guid id, [FromBody] Guid? versionId, ISender sender) =>
            MapResult(await sender.Send(new PublishCommunicationTemplateCommand(id, versionId))))
            .RequirePermission(PermissionConstants.Communication.ManageTemplates);

        group.MapPost("templates/{id:guid}/preview", async Task<IResult> (Guid id, [FromBody] PreviewTemplateRequest request, ISender sender) =>
            MapResult(await sender.Send(new PreviewCommunicationTemplateQuery(id, request.VersionId, request.Variables))))
            .RequirePermission(PermissionConstants.Communication.View);

        group.MapGet("messages", async Task<IResult> (
                [FromQuery] int pageNumber, [FromQuery] int pageSize, [FromQuery] string? status,
                [FromQuery] string? channel, [FromQuery] string? category, [FromQuery] string? search,
                ISender sender) =>
            MapResult(await sender.Send(new GetCommunicationMessagesQuery(
                pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? 20 : pageSize, status, channel, category, search))))
            .RequirePermission(PermissionConstants.Communication.View);

        group.MapGet("messages/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetCommunicationMessageQuery(id))))
            .RequirePermission(PermissionConstants.Communication.View);

        group.MapGet("failed-deliveries", async Task<IResult> ([FromQuery] int pageNumber, [FromQuery] int pageSize, ISender sender) =>
            MapResult(await sender.Send(new GetFailedCommunicationDeliveriesQuery(
                pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? 20 : pageSize))))
            .RequirePermission(PermissionConstants.Communication.View);

        group.MapPost("failed-deliveries/{id:guid}/retry", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new RetryCommunicationDeliveryCommand(id))))
            .RequirePermission(PermissionConstants.Communication.Manage);

        group.MapGet("providers", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetCommunicationProvidersQuery())))
            .RequirePermission(PermissionConstants.Communication.View);

        group.MapPut("providers/{id:guid}", async Task<IResult> (Guid id, [FromBody] UpdateCommunicationProviderRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateCommunicationProviderCommand(id, request))))
            .RequirePermission(PermissionConstants.Communication.ManageProviders);

        group.MapGet("audit-log", async Task<IResult> ([FromQuery] int pageNumber, [FromQuery] int pageSize, ISender sender) =>
            MapResult(await sender.Send(new GetCommunicationAuditLogQuery(
                pageNumber < 1 ? 1 : pageNumber, pageSize < 1 ? 20 : pageSize))))
            .RequirePermission(PermissionConstants.Communication.View);
    }

    public static void MapCommunicationPreferenceEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/communication/preferences")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Communication")
            .HasApiVersion(1)
            .RequireCustomerContext();

        group.MapGet("", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetCommunicationPreferencesQuery())));

        group.MapPut("", async Task<IResult> ([FromBody] CommunicationPreferencesDto request, ISender sender) =>
            MapResult(await sender.Send(new UpdateCommunicationPreferencesCommand(request))));
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}

internal sealed record PreviewTemplateRequest(Guid? VersionId, IReadOnlyDictionary<string, string> Variables);
