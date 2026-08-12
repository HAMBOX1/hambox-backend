using Asp.Versioning.Builder;
using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaq;
using HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaqCategory;
using HAMBOX.Modules.Content.Application.Features.Faqs.DeleteFaq;
using HAMBOX.Modules.Content.Application.Features.Faqs.DuplicateFaq;
using HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqById;
using HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqCategories;
using HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqs;
using HAMBOX.Modules.Content.Application.Features.Faqs.GetPublishedFaqs;
using HAMBOX.Modules.Content.Application.Features.Faqs.ReorderFaqs;
using HAMBOX.Modules.Content.Application.Features.Faqs.SetFaqPublishState;
using HAMBOX.Modules.Content.Application.Features.Faqs.UpdateFaq;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Content.Presentation.Endpoints;

internal static class FaqEndpoints
{
    public static void MapFaqEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var admin = app.MapGroup("api/v{version:apiVersion}/faqs")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("FAQ")
            .HasApiVersion(1)
            .RequireAuthorization();

        admin.MapGet("", async Task<IResult> (
                int? pageNumber, int? pageSize, string? searchTerm, FaqScope? scope, Guid? categoryId, bool? isPublished, ISender sender) =>
            MapResult(await sender.Send(new GetFaqsQuery(
                pageNumber ?? 1, pageSize ?? 20, searchTerm, scope, categoryId, isPublished))))
            .RequirePermission(PermissionConstants.Faq.View);

        admin.MapGet("categories", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetFaqCategoriesQuery())))
            .RequirePermission(PermissionConstants.Faq.View);

        admin.MapPost("categories", async Task<IResult> ([FromBody] CreateFaqCategoryRequest request, ISender sender) =>
            MapResult(await sender.Send(new CreateFaqCategoryCommand(request.NameEn, request.NameAr)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Faq.Edit);

        admin.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetFaqByIdQuery(id))))
            .RequirePermission(PermissionConstants.Faq.View);

        admin.MapPost("", async Task<IResult> ([FromBody] CreateFaqRequest request, ISender sender) =>
            MapResult(
                await sender.Send(new CreateFaqCommand(
                    request.QuestionEn, request.QuestionAr, request.AnswerEn, request.AnswerAr,
                    request.CategoryId, request.Scope, request.TargetId, request.SortOrder)),
                StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Faq.Create);

        admin.MapPut("{id:guid}", async Task<IResult> (Guid id, [FromBody] UpdateFaqRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateFaqCommand(
                id, request.QuestionEn, request.QuestionAr, request.AnswerEn, request.AnswerAr,
                request.CategoryId, request.Scope, request.TargetId))))
            .RequirePermission(PermissionConstants.Faq.Edit);

        admin.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteFaqCommand(id)), StatusCodes.Status204NoContent))
            .RequirePermission(PermissionConstants.Faq.Delete);

        admin.MapPost("{id:guid}/publish", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new SetFaqPublishStateCommand(id, true))))
            .RequirePermission(PermissionConstants.Faq.Publish);

        admin.MapPost("{id:guid}/unpublish", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new SetFaqPublishStateCommand(id, false))))
            .RequirePermission(PermissionConstants.Faq.Publish);

        admin.MapPost("{id:guid}/duplicate", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DuplicateFaqCommand(id)), StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Faq.Create);

        admin.MapPut("reorder", async Task<IResult> ([FromBody] ReorderFaqsRequest request, ISender sender) =>
            MapResult(await sender.Send(new ReorderFaqsCommand(request.Entries))))
            .RequirePermission(PermissionConstants.Faq.Edit);

        var publicGroup = app.MapGroup("api/v{version:apiVersion}/faq")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("FAQ")
            .HasApiVersion(1)
            .AllowAnonymous();

        // Serves the /faq hub (Scope=Global, no targetId), and Product/Category marketing pages
        // (Scope+targetId) — always includes Global FAQs alongside the requested scope. Never exposes
        // unpublished/deleted rows (enforced in the query, not here).
        publicGroup.MapGet("published", async Task<IResult> (FaqScope? scope, Guid? targetId, ISender sender) =>
            MapResult(await sender.Send(new GetPublishedFaqsQuery(scope ?? FaqScope.Global, targetId))));

        publicGroup.MapGet("categories", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetFaqCategoriesQuery())));
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}

internal sealed record CreateFaqRequest(
    string QuestionEn,
    string? QuestionAr,
    string AnswerEn,
    string? AnswerAr,
    Guid CategoryId,
    FaqScope Scope,
    Guid? TargetId,
    int SortOrder = 0);

internal sealed record UpdateFaqRequest(
    string QuestionEn,
    string? QuestionAr,
    string AnswerEn,
    string? AnswerAr,
    Guid CategoryId,
    FaqScope Scope,
    Guid? TargetId);

internal sealed record ReorderFaqsRequest(IReadOnlyList<FaqReorderEntry> Entries);

internal sealed record CreateFaqCategoryRequest(string NameEn, string? NameAr);
