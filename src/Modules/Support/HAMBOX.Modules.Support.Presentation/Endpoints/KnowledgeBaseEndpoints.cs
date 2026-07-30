using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.CreateKnowledgeArticle;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.CreateKnowledgeCategory;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.DeleteKnowledgeArticle;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.DeleteKnowledgeCategory;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeArticleById;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeArticles;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeCategories;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.PublishKnowledgeArticle;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UnpublishKnowledgeArticle;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UpdateKnowledgeArticle;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UpdateKnowledgeCategory;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Support.Presentation.Endpoints;

internal static class KnowledgeBaseEndpoints
{
    public static void MapKnowledgeBaseEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        // Public storefront KB — anonymous, Published + Public articles only.
        var publicGroup = app.MapGroup("api/v{version:apiVersion}/knowledge-base")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Knowledge Base")
            .HasApiVersion(1)
            .AllowAnonymous();

        publicGroup.MapGet("categories", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetKnowledgeCategoriesQuery())));

        publicGroup.MapGet("articles", async Task<IResult> (
            [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? search, [FromQuery] Guid? categoryId,
            ISender sender) =>
            MapResult(await sender.Send(new GetKnowledgeArticlesQuery(true, page ?? 1, pageSize ?? 20, search, categoryId, null))));

        publicGroup.MapGet("articles/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetKnowledgeArticleByIdQuery(id, true))));

        // Admin authoring — permission-gated.
        var adminGroup = app.MapGroup("api/v{version:apiVersion}/support/admin/knowledge-base")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Support Admin")
            .HasApiVersion(1)
            .RequirePermission(PermissionConstants.Support.ManageKnowledgeBase);

        adminGroup.MapGet("articles", async Task<IResult> (
            [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? search,
            [FromQuery] Guid? categoryId, [FromQuery] KnowledgeArticleStatus? status, ISender sender) =>
            MapResult(await sender.Send(new GetKnowledgeArticlesQuery(false, page ?? 1, pageSize ?? 20, search, categoryId, status))));

        adminGroup.MapGet("articles/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetKnowledgeArticleByIdQuery(id, false))));

        adminGroup.MapPost("articles", async Task<IResult> ([FromBody] CreateKnowledgeArticleCommand command, ISender sender) =>
            MapResult(await sender.Send(command), StatusCodes.Status201Created));

        adminGroup.MapPut("articles/{id:guid}", async Task<IResult> (
            Guid id, [FromBody] UpdateArticleRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateKnowledgeArticleCommand(
                id, request.CategoryId, request.Title, request.Body, request.Visibility, request.RelatedArticleIds))));

        adminGroup.MapDelete("articles/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteKnowledgeArticleCommand(id)), StatusCodes.Status204NoContent));

        adminGroup.MapPost("articles/{id:guid}/publish", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new PublishKnowledgeArticleCommand(id))));

        adminGroup.MapPost("articles/{id:guid}/unpublish", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new UnpublishKnowledgeArticleCommand(id))));

        adminGroup.MapPost("categories", async Task<IResult> ([FromBody] CreateKnowledgeCategoryCommand command, ISender sender) =>
            MapResult(await sender.Send(command), StatusCodes.Status201Created));

        adminGroup.MapPut("categories/{id:guid}", async Task<IResult> (
            Guid id, [FromBody] UpdateKnowledgeCategoryRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateKnowledgeCategoryCommand(id, request.Name, request.SortOrder, request.IsActive))));

        adminGroup.MapDelete("categories/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteKnowledgeCategoryCommand(id)), StatusCodes.Status204NoContent));
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}

internal sealed record UpdateArticleRequest(
    Guid CategoryId, string Title, string Body, KnowledgeArticleVisibility Visibility, IReadOnlyList<Guid>? RelatedArticleIds);

internal sealed record UpdateKnowledgeCategoryRequest(string Name, int SortOrder, bool IsActive);
