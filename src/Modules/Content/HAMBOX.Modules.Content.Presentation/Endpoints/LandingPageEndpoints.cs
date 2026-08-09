using Asp.Versioning.Builder;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Features.LandingPages.ActivateLandingPageTemplate;
using HAMBOX.Modules.Content.Application.Features.LandingPages.CreateLandingPageTemplate;
using HAMBOX.Modules.Content.Application.Features.LandingPages.DeleteLandingPageTemplate;
using HAMBOX.Modules.Content.Application.Features.LandingPages.DiscardLandingPageTemplateDraft;
using HAMBOX.Modules.Content.Application.Features.LandingPages.GetLandingPageTemplateById;
using HAMBOX.Modules.Content.Application.Features.LandingPages.GetLandingPageTemplates;
using HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedLandingPage;
using HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedLandingPageFooter;
using HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedProductMarketingPageAvailability;
using HAMBOX.Modules.Content.Application.Features.LandingPages.PublishLandingPageTemplate;
using HAMBOX.Modules.Content.Application.Features.LandingPages.SaveLandingPageTemplateDraft;
using HAMBOX.Modules.Content.Application.Features.LandingPages.UploadLandingPageImage;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Content.Presentation.Endpoints;

internal static class LandingPageEndpoints
{
    public static void MapLandingPageTemplateEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var admin = app.MapGroup("api/v{version:apiVersion}/page-builder/templates")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Page Builder")
            .HasApiVersion(1)
            .RequireAuthorization();

        admin.MapGet("", async Task<IResult> (LandingPageScope? scope, Guid[]? targetIds, ISender sender) =>
            MapResult(await sender.Send(new GetLandingPageTemplatesQuery(scope, targetIds))))
            .RequirePermission(PermissionConstants.PageBuilder.View);

        admin.MapPost("", async Task<IResult> ([FromBody] CreateLandingPageTemplateRequest request, ISender sender) =>
            MapResult(
                await sender.Send(new CreateLandingPageTemplateCommand(
                    request.Name, request.CloneFromTemplateId, request.Scope, request.TargetId)),
                StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.PageBuilder.Edit);

        admin.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetLandingPageTemplateByIdQuery(id))))
            .RequirePermission(PermissionConstants.PageBuilder.View);

        admin.MapPut("{id:guid}/draft", async Task<IResult> (Guid id, [FromBody] SaveLandingPageTemplateDraftRequest request, ISender sender) =>
            MapResult(await sender.Send(new SaveLandingPageTemplateDraftCommand(
                id, request.Sections, request.SeoTitle, request.SeoDescription, request.SeoOgImageUrl))))
            .RequirePermission(PermissionConstants.PageBuilder.Edit);

        admin.MapPost("{id:guid}/publish", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new PublishLandingPageTemplateCommand(id))))
            .RequirePermission(PermissionConstants.PageBuilder.Publish);

        admin.MapPost("{id:guid}/discard-draft", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DiscardLandingPageTemplateDraftCommand(id))))
            .RequirePermission(PermissionConstants.PageBuilder.Edit);

        admin.MapPost("{id:guid}/activate", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new ActivateLandingPageTemplateCommand(id))))
            .RequirePermission(PermissionConstants.PageBuilder.Publish);

        admin.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteLandingPageTemplateCommand(id)), StatusCodes.Status204NoContent))
            .RequirePermission(PermissionConstants.PageBuilder.Edit);

        admin.MapPost("images", async Task<IResult> (IFormFile file, ISender sender) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadLandingPageImageCommand(
                stream, file.FileName, file.ContentType, file.Length));

            return result.IsSuccess
                ? Results.Json(new { url = result.Value }, statusCode: StatusCodes.Status201Created)
                : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
        })
            .RequirePermission(PermissionConstants.PageBuilder.Edit)
            .DisableAntiforgery();

        var publicGroup = app.MapGroup("api/v{version:apiVersion}/page-builder")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Page Builder")
            .HasApiVersion(1)
            .AllowAnonymous();

        publicGroup.MapGet("published", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetPublishedLandingPageQuery())));

        // Product/Category marketing pages: absence (no published+active page for this target) is the
        // expected, common case — mapped to 404 rather than the admin surface's generic 400, so the
        // storefront can cleanly distinguish "fall back to the normal PDP/category page" from a real error.
        publicGroup.MapGet("published/product/{productId:guid}", async Task<IResult> (Guid productId, ISender sender) =>
            MapPublishedTargetResult(await sender.Send(new GetPublishedLandingPageQuery(LandingPageScope.Product, productId))));

        // Batched "which of these products have a live marketing page" check for storefront product
        // cards — avoids one request per card and never exposes draft/inactive pages (see handler).
        publicGroup.MapGet("published/product/availability", async Task<IResult> (Guid[] ids, ISender sender) =>
            MapResult(await sender.Send(new GetPublishedProductMarketingPageAvailabilityQuery(ids))));

        publicGroup.MapGet("published/category/{categoryId:guid}", async Task<IResult> (Guid categoryId, ISender sender) =>
            MapPublishedTargetResult(await sender.Send(new GetPublishedLandingPageQuery(LandingPageScope.Category, categoryId))));

        publicGroup.MapGet("published/footer", async Task<IResult> (ISender sender) =>
        {
            var result = await sender.Send(new GetPublishedLandingPageFooterQuery());
            if (result.IsFailure)
            {
                return Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
            }

            return result.Value.Section is null
                ? Results.NoContent()
                : Results.Json(result.Value.Section, statusCode: StatusCodes.Status200OK);
        });
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapPublishedTargetResult(Result<PublishedLandingPageDto> result) =>
        result.IsSuccess ? Results.Json(result.Value) : Results.NotFound();
}

internal sealed record CreateLandingPageTemplateRequest(
    string Name,
    Guid? CloneFromTemplateId,
    LandingPageScope Scope = LandingPageScope.Homepage,
    Guid? TargetId = null);

internal sealed record SaveLandingPageTemplateDraftRequest(
    IReadOnlyList<LandingPageSectionEntry> Sections,
    string? SeoTitle = null,
    string? SeoDescription = null,
    string? SeoOgImageUrl = null);
