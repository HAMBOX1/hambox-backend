using Asp.Versioning.Builder;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Themes.Application.Contracts.Themes;
using HAMBOX.Modules.Themes.Application.Features.Themes;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Themes.Presentation.Endpoints;

internal static class ThemeEndpoints
{
    public static void MapThemeEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var admin = app.MapGroup("api/v{version:apiVersion}/themes")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Themes")
            .HasApiVersion(1)
            .RequireAuthorization();

        admin.MapGet("", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : pageSize;
            return MapResult(await sender.Send(new GetThemesQuery(pageNumber, pageSize, searchTerm, status)));
        }).RequirePermission(PermissionConstants.Themes.View);

        admin.MapGet("statistics", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetThemeStatisticsQuery())))
            .RequirePermission(PermissionConstants.Themes.View);

        admin.MapGet("templates", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetThemeTemplatesQuery())))
            .RequirePermission(PermissionConstants.Themes.View);

        admin.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetThemeByIdQuery(id))))
            .RequirePermission(PermissionConstants.Themes.View);

        admin.MapPost("", async Task<IResult> ([FromBody] CreateThemeRequest request, ISender sender) =>
            MapResult(await sender.Send(new CreateThemeCommand(request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Themes.Create);

        admin.MapPut("{id:guid}", async Task<IResult> (Guid id, [FromBody] UpdateThemeRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateThemeCommand(id, request))))
            .RequirePermission(PermissionConstants.Themes.Edit);

        admin.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteThemeCommand(id)), StatusCodes.Status204NoContent))
            .RequirePermission(PermissionConstants.Themes.Delete);

        admin.MapPost("{id:guid}/duplicate", async Task<IResult> (Guid id, [FromBody] DuplicateThemeRequest request, ISender sender) =>
            MapResult(await sender.Send(new DuplicateThemeCommand(id, request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Themes.Create);

        admin.MapPost("{id:guid}/publish", async Task<IResult> (Guid id, [FromBody] Guid? versionId, ISender sender) =>
            MapResult(await sender.Send(new PublishThemeCommand(id, versionId))))
            .RequirePermission(PermissionConstants.Themes.Publish);

        admin.MapPost("{id:guid}/archive", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new ArchiveThemeCommand(id))))
            .RequirePermission(PermissionConstants.Themes.Edit);

        admin.MapPost("{id:guid}/rollback", async Task<IResult> (Guid id, [FromBody] Guid versionId, ISender sender) =>
            MapResult(await sender.Send(new RollbackThemeCommand(id, versionId))))
            .RequirePermission(PermissionConstants.Themes.Rollback);

        admin.MapPost("{id:guid}/preview", async Task<IResult> (Guid id, [FromBody] Guid? versionId, ISender sender) =>
            MapResult(await sender.Send(new CreatePreviewSessionCommand(id, versionId)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Themes.View);

        admin.MapPost("{id:guid}/schedule", async Task<IResult> (Guid id, [FromBody] ScheduleThemeRequest request, ISender sender) =>
            MapResult(await sender.Send(new ScheduleThemeCommand(id, request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Themes.Schedule);

        admin.MapPost("{id:guid}/assign", async Task<IResult> (Guid id, [FromBody] AssignThemeRequest request, ISender sender) =>
            MapResult(await sender.Send(new AssignThemeCommand(id, request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Themes.Assign);

        admin.MapPut("{id:guid}/assets", async Task<IResult> (Guid id, [FromBody] IReadOnlyList<ThemeAssetRequest> assets, ISender sender) =>
            MapResult(await sender.Send(new UpsertThemeAssetsCommand(id, assets))))
            .RequirePermission(PermissionConstants.Themes.Edit);

        admin.MapGet("{id:guid}/history", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetThemeHistoryQuery(id))))
            .RequirePermission(PermissionConstants.Themes.View);

        admin.MapGet("compare", async Task<IResult> ([FromQuery] Guid leftId, [FromQuery] Guid rightId, ISender sender) =>
            MapResult(await sender.Send(new CompareThemesQuery(leftId, rightId))))
            .RequirePermission(PermissionConstants.Themes.View);

        admin.MapGet("{id:guid}/export", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new ExportThemeQuery(id))))
            .RequirePermission(PermissionConstants.Themes.Export);

        admin.MapPost("import", async Task<IResult> ([FromBody] ImportThemeRequest request, ISender sender) =>
            MapResult(await sender.Send(new ImportThemeCommand(request)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Themes.Import);

        var publicGroup = app.MapGroup("api/v{version:apiVersion}/themes")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Themes")
            .HasApiVersion(1);

        publicGroup.MapGet("active", async Task<IResult> ([FromQuery] string? membershipPlanSlug, ISender sender) =>
            MapResult(await sender.Send(new GetActiveThemeQuery(membershipPlanSlug))));

        publicGroup.MapGet("preview/{token}", async Task<IResult> (string token, ISender sender) =>
            MapResult(await sender.Send(new GetPreviewThemeQuery(token))));
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}
