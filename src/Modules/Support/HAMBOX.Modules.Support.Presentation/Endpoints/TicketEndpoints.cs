using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Support.Application.Features.Tags.AddTagToTicket;
using HAMBOX.Modules.Support.Application.Features.Tags.RemoveTagFromTicket;
using HAMBOX.Modules.Support.Application.Features.Tickets.AssignTicket;
using HAMBOX.Modules.Support.Application.Features.Tickets.ChangeTicketPriority;
using HAMBOX.Modules.Support.Application.Features.Tickets.ChangeTicketStatus;
using HAMBOX.Modules.Support.Application.Features.Tickets.CloseTicket;
using HAMBOX.Modules.Support.Application.Features.Tickets.CreateTicket;
using HAMBOX.Modules.Support.Application.Features.Tickets.DeleteTicket;
using HAMBOX.Modules.Support.Application.Features.Tickets.GetTicketById;
using HAMBOX.Modules.Support.Application.Features.Tickets.GetTickets;
using HAMBOX.Modules.Support.Application.Features.Tickets.MergeTickets;
using HAMBOX.Modules.Support.Application.Features.Tickets.RateTicket;
using HAMBOX.Modules.Support.Application.Features.Tickets.ReopenTicket;
using HAMBOX.Modules.Support.Application.Features.Tickets.ReplyToTicket;
using HAMBOX.Modules.Support.Application.Features.Tickets.UploadTicketAttachment;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Support.Presentation.Endpoints;

internal static class TicketEndpoints
{
    public static void MapTicketEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        MapCustomerEndpoints(app, apiVersionSet);
        MapAdminEndpoints(app, apiVersionSet);
    }

    private static void MapCustomerEndpoints(IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/support/tickets")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Support")
            .HasApiVersion(1)
            .RequireAuthorization()
            .RequireCustomerContext();

        group.MapGet("", async Task<IResult> (
            [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? search,
            [FromQuery] TicketStatus? status, [FromQuery] Guid? categoryId, [FromQuery] Guid? priorityId,
            [FromQuery] Guid? tagId, [FromQuery] string? sortBy, [FromQuery] bool? sortDescending,
            ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new GetTicketsQuery(
                currentUser.UserId, page ?? 1, pageSize ?? 20, search, status,
                categoryId, priorityId, null, tagId, null,
                sortBy ?? "createdOnUtc", sortDescending ?? true))));

        group.MapGet("{id:guid}", async Task<IResult> (Guid id, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new GetTicketByIdQuery(id, currentUser.UserId))));

        group.MapPost("", async Task<IResult> (
            [FromBody] CreateTicketRequest request, ICurrentUserService currentUser, ISender sender) =>
            MapResult(
                await sender.Send(new CreateTicketCommand(
                    currentUser.UserId!, currentUser.UserId!, request.Subject, request.Body,
                    request.CategoryId, request.PriorityId, request.RelatedOrderId, request.RelatedProductId,
                    request.CustomerCountry, request.CustomerBrowser, request.CustomerDevice, null)),
                StatusCodes.Status201Created));

        group.MapPost("{id:guid}/messages", async Task<IResult> (
            Guid id, [FromBody] ReplyRequest request, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new ReplyToTicketCommand(
                id, currentUser.UserId!, TicketMessageAuthorRole.Customer, request.Body, false, request.AttachmentIds, null))));

        group.MapPost("{id:guid}/close", async Task<IResult> (Guid id, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new CloseTicketCommand(id, currentUser.UserId!, true))));

        group.MapPost("{id:guid}/reopen", async Task<IResult> (Guid id, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new ReopenTicketCommand(id, currentUser.UserId!, true))));

        group.MapPost("{id:guid}/rate", async Task<IResult> (
            Guid id, [FromBody] RateTicketRequest request, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new RateTicketCommand(id, currentUser.UserId!, request.Score, request.Comment))));

        group.MapPost("{id:guid}/attachments", async Task<IResult> (
            Guid id, IFormFile file, ICurrentUserService currentUser, ISender sender) =>
        {
            if (file.Length <= 0)
            {
                return Results.BadRequest(new ProblemDetails { Title = "A file is required.", Status = 400 });
            }

            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadTicketAttachmentCommand(
                id, currentUser.UserId!, stream, file.FileName, file.ContentType, file.Length));
            return MapResult(result, StatusCodes.Status201Created);
        }).DisableAntiforgery();
    }

    private static void MapAdminEndpoints(IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/support/admin/tickets")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Support Admin")
            .HasApiVersion(1);

        group.MapGet("", async Task<IResult> (
            [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? search,
            [FromQuery] TicketStatus? status, [FromQuery] Guid? categoryId, [FromQuery] Guid? priorityId,
            [FromQuery] string? assignedAgentUserId, [FromQuery] Guid? tagId, [FromQuery] bool? unassigned,
            [FromQuery] string? sortBy, [FromQuery] bool? sortDescending, ISender sender) =>
            MapResult(await sender.Send(new GetTicketsQuery(
                null, page ?? 1, pageSize ?? 20, search, status,
                categoryId, priorityId, assignedAgentUserId, tagId, unassigned,
                sortBy ?? "createdOnUtc", sortDescending ?? true))))
            .RequirePermission(PermissionConstants.Support.View);

        group.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetTicketByIdQuery(id, null))))
            .RequirePermission(PermissionConstants.Support.View);

        group.MapPost("", async Task<IResult> (
            [FromBody] CreateAdminTicketRequest request, ICurrentUserService currentUser, ISender sender) =>
            MapResult(
                await sender.Send(new CreateTicketCommand(
                    request.CustomerUserId, currentUser.UserId!, request.Subject, request.Body,
                    request.CategoryId, request.PriorityId, request.RelatedOrderId, request.RelatedProductId,
                    null, null, null, null)),
                StatusCodes.Status201Created))
            .RequirePermission(PermissionConstants.Support.Create);

        group.MapPost("{id:guid}/messages", async Task<IResult> (
            Guid id, [FromBody] AdminReplyRequest request, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new ReplyToTicketCommand(
                id, currentUser.UserId!, TicketMessageAuthorRole.Agent, request.Body, request.IsInternal,
                request.AttachmentIds, request.SavedReplyId))))
            .RequirePermission(PermissionConstants.Support.Reply);

        group.MapPost("{id:guid}/assign", async Task<IResult> (
            Guid id, [FromBody] AssignTicketRequest request, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new AssignTicketCommand(id, request.AgentUserId, currentUser.UserId!))))
            .RequirePermission(PermissionConstants.Support.Assign);

        group.MapPost("{id:guid}/status", async Task<IResult> (
            Guid id, [FromBody] ChangeStatusRequest request, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new ChangeTicketStatusCommand(id, request.Status, currentUser.UserId!))))
            .RequirePermission(PermissionConstants.Support.Reply);

        group.MapPost("{id:guid}/priority", async Task<IResult> (
            Guid id, [FromBody] ChangePriorityRequest request, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new ChangeTicketPriorityCommand(id, request.PriorityId, currentUser.UserId!))))
            .RequirePermission(PermissionConstants.Support.Reply);

        group.MapPost("{id:guid}/close", async Task<IResult> (Guid id, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new CloseTicketCommand(id, currentUser.UserId!, false))))
            .RequirePermission(PermissionConstants.Support.Close);

        group.MapPost("{id:guid}/reopen", async Task<IResult> (Guid id, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new ReopenTicketCommand(id, currentUser.UserId!, false))))
            .RequirePermission(PermissionConstants.Support.Close);

        group.MapPost("{id:guid}/merge", async Task<IResult> (
            Guid id, [FromBody] MergeTicketRequest request, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new MergeTicketsCommand(id, request.TargetTicketId, currentUser.UserId!))))
            .RequirePermission(PermissionConstants.Support.Close);

        group.MapDelete("{id:guid}", async Task<IResult> (Guid id, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new DeleteTicketCommand(id, currentUser.UserId!)), StatusCodes.Status204NoContent))
            .RequirePermission(PermissionConstants.Support.Delete);

        group.MapPost("{id:guid}/attachments", async Task<IResult> (
            Guid id, IFormFile file, ICurrentUserService currentUser, ISender sender) =>
        {
            if (file.Length <= 0)
            {
                return Results.BadRequest(new ProblemDetails { Title = "A file is required.", Status = 400 });
            }

            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadTicketAttachmentCommand(
                id, currentUser.UserId!, stream, file.FileName, file.ContentType, file.Length));
            return MapResult(result, StatusCodes.Status201Created);
        }).RequirePermission(PermissionConstants.Support.Reply).DisableAntiforgery();

        group.MapPost("{id:guid}/tags/{tagId:guid}", async Task<IResult> (
            Guid id, Guid tagId, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new AddTagToTicketCommand(id, tagId, currentUser.UserId!))))
            .RequirePermission(PermissionConstants.Support.Reply);

        group.MapDelete("{id:guid}/tags/{tagId:guid}", async Task<IResult> (
            Guid id, Guid tagId, ICurrentUserService currentUser, ISender sender) =>
            MapResult(await sender.Send(new RemoveTagFromTicketCommand(id, tagId, currentUser.UserId!))))
            .RequirePermission(PermissionConstants.Support.Reply);
    }

    private static IResult MapResult(Result result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.StatusCode(successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess ? Results.Json(result.Value, statusCode: successStatusCode) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}

internal sealed record CreateTicketRequest(
    string Subject, string Body, Guid? CategoryId, Guid? PriorityId, Guid? RelatedOrderId, Guid? RelatedProductId,
    string? CustomerCountry, string? CustomerBrowser, string? CustomerDevice);

internal sealed record CreateAdminTicketRequest(
    string CustomerUserId, string Subject, string Body, Guid? CategoryId, Guid? PriorityId,
    Guid? RelatedOrderId, Guid? RelatedProductId);

internal sealed record ReplyRequest(string Body, IReadOnlyList<Guid>? AttachmentIds);

internal sealed record AdminReplyRequest(
    string Body, bool IsInternal, IReadOnlyList<Guid>? AttachmentIds, Guid? SavedReplyId);

internal sealed record RateTicketRequest(int Score, string? Comment);

internal sealed record AssignTicketRequest(string AgentUserId);

internal sealed record ChangeStatusRequest(TicketStatus Status);

internal sealed record ChangePriorityRequest(Guid? PriorityId);

internal sealed record MergeTicketRequest(Guid TargetTicketId);
