using Asp.Versioning.Builder;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Legal.Application.Contracts.Legal;
using HAMBOX.Modules.Legal.Application.Features.Legal;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Legal.Presentation.Endpoints;

internal static class LegalEndpoints
{
    public static void MapLegalDocumentEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var admin = app.MapGroup("api/v{version:apiVersion}/legal/documents")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Legal")
            .HasApiVersion(1)
            .RequireAuthorization();

        admin.MapGet("", async Task<IResult> (
                string? search, string? category, string? status,
                bool? showInFooter, bool? showInNavigation, bool? requireAcceptance,
                ISender sender) =>
            {
                var filter = new LegalSectionQueryFilter(search, category, status, showInFooter, showInNavigation, requireAcceptance);
                return MapResult(await sender.Send(new GetLegalSectionsQuery(filter)));
            })
            .RequirePermission(PermissionConstants.Legal.View);

        admin.MapGet("{slug}", async Task<IResult> (string slug, ISender sender) =>
            MapResult(await sender.Send(new GetLegalSectionBySlugQuery(slug))))
            .RequirePermission(PermissionConstants.Legal.View);

        admin.MapGet("{slug}/history", async Task<IResult> (string slug, ISender sender) =>
            MapResult(await sender.Send(new GetLegalSectionHistoryQuery(slug))))
            .RequirePermission(PermissionConstants.Legal.View);

        admin.MapPost("", async Task<IResult> ([FromBody] CreateLegalSectionRequest request, ISender sender) =>
            MapResult(await sender.Send(new CreateLegalSectionCommand(request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Legal.Create);

        admin.MapPut("{slug}", async Task<IResult> (string slug, [FromBody] UpdateLegalSectionRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateLegalSectionCommand(slug, request))))
            .RequirePermission(PermissionConstants.Legal.Edit);

        admin.MapPost("{slug}/publish", async Task<IResult> (string slug, [FromBody] Guid? versionId, ISender sender) =>
            MapResult(await sender.Send(new PublishLegalSectionCommand(slug, versionId))))
            .RequirePermission(PermissionConstants.Legal.Publish);

        admin.MapPost("{slug}/archive", async Task<IResult> (string slug, [FromBody] ArchiveLegalSectionRequest request, ISender sender) =>
            MapResult(await sender.Send(new ArchiveLegalSectionCommand(slug, request.Archived))))
            .RequirePermission(PermissionConstants.Legal.Edit);

        admin.MapPost("{slug}/duplicate", async Task<IResult> (string slug, [FromBody] DuplicateLegalSectionRequest request, ISender sender) =>
            MapResult(await sender.Send(new DuplicateLegalSectionCommand(slug, request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Legal.Create);

        admin.MapDelete("{slug}", async Task<IResult> (string slug, ISender sender) =>
            MapResult(await sender.Send(new DeleteLegalSectionCommand(slug))))
            .RequirePermission(PermissionConstants.Legal.Delete);

        var publicGroup = app.MapGroup("api/v{version:apiVersion}/legal/public/documents")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Legal")
            .HasApiVersion(1)
            .AllowAnonymous();

        publicGroup.MapGet("", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetPublishedLegalSectionsQuery())));

        publicGroup.MapGet("{slug}", async Task<IResult> (string slug, ISender sender) =>
            MapResult(await sender.Send(new GetPublishedLegalSectionBySlugQuery(slug))));

        var acceptance = app.MapGroup("api/v{version:apiVersion}/legal/acceptance")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Legal")
            .HasApiVersion(1)
            .RequireAuthorization();

        acceptance.MapGet("status", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetLegalAcceptanceStatusQuery())));

        acceptance.MapPost("", async Task<IResult> (HttpContext httpContext, ISender sender) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";
            var language = httpContext.Request.Headers["Accept-Language"].ToString() is { Length: > 0 } acceptLanguage
                ? acceptLanguage.Split(',')[0].Split(';')[0].Trim()
                : "en";

            return MapResult(await sender.Send(new RecordLegalAcceptanceCommand(ipAddress, userAgent, language)));
        });
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}
