using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Application.Features.Memberships.Plans;
using HAMBOX.Modules.Commerce.Application.Features.Memberships.Plans.GetMembershipPlans;
using HAMBOX.Modules.Commerce.Application.Features.Memberships.Subscriptions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class MembershipEndpoints
{
    public static void MapMembershipEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/memberships")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Memberships")
            .HasApiVersion(1)
            .RequireAuthorization();

        var plans = group.MapGroup("/plans");

        plans.MapGet("", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;
            return MapResult(await sender.Send(new GetMembershipPlansQuery(pageNumber, pageSize, searchTerm, status)));
        })
        .WithName("GetMembershipPlans")
        .RequirePermission(PermissionConstants.Memberships.View);

        plans.MapGet("compare", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetPlanComparisonQuery())))
        .WithName("GetMembershipPlanComparison")
        .RequirePermission(PermissionConstants.Memberships.View);

        plans.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new GetMembershipPlanByIdQuery(id))))
        .WithName("GetMembershipPlanById")
        .RequirePermission(PermissionConstants.Memberships.View);

        plans.MapPost("", async Task<IResult> ([FromBody] CreateMembershipPlanRequest request, ISender sender) =>
            MapResult(await sender.Send(new CreateMembershipPlanCommand(request)), StatusCodes.Status201Created))
        .WithName("CreateMembershipPlan")
        .RequirePermission(PermissionConstants.Memberships.Create);

        plans.MapPut("{id:guid}", async Task<IResult> (Guid id, [FromBody] UpdateMembershipPlanRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateMembershipPlanCommand(id, request))))
        .WithName("UpdateMembershipPlan")
        .RequirePermission(PermissionConstants.Memberships.Edit);

        plans.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new DeleteMembershipPlanCommand(id)), StatusCodes.Status204NoContent))
        .WithName("DeleteMembershipPlan")
        .RequirePermission(PermissionConstants.Memberships.Delete);

        plans.MapPost("{id:guid}/duplicate", async Task<IResult> (
            Guid id,
            [FromBody] DuplicateMembershipPlanRequest? request,
            ISender sender) =>
            MapResult(
                await sender.Send(new DuplicateMembershipPlanCommand(id, request?.NewName, request?.NewSlug)),
                StatusCodes.Status201Created))
        .WithName("DuplicateMembershipPlan")
        .RequirePermission(PermissionConstants.Memberships.Create);

        plans.MapPost("{id:guid}/archive", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new ArchiveMembershipPlanCommand(id))))
        .WithName("ArchiveMembershipPlan")
        .RequirePermission(PermissionConstants.Memberships.Edit);

        plans.MapPost("{id:guid}/activate", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new ActivateMembershipPlanCommand(id))))
        .WithName("ActivateMembershipPlan")
        .RequirePermission(PermissionConstants.Memberships.Edit);

        group.MapGet("statistics", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetMembershipStatisticsQuery())))
        .WithName("GetMembershipStatistics")
        .RequirePermission(PermissionConstants.Memberships.View);

        group.MapGet("members", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : pageSize;
            return MapResult(await sender.Send(new GetMembershipMembersQuery(pageNumber, pageSize, searchTerm)));
        })
        .WithName("GetMembershipMembers")
        .RequirePermission(PermissionConstants.Memberships.View);

        group.MapPost("assign", async Task<IResult> ([FromBody] AssignMembershipRequest request, ISender sender) =>
            MapResult(await sender.Send(new AssignMembershipCommand(request)), StatusCodes.Status201Created))
        .WithName("AssignMembership")
        .RequirePermission(PermissionConstants.Memberships.Assign);

        group.MapPost("assign/bulk", async Task<IResult> ([FromBody] BulkAssignMembershipRequest request, ISender sender) =>
            MapResult(await sender.Send(new BulkAssignMembershipCommand(request))))
        .WithName("BulkAssignMembership")
        .RequirePermission(PermissionConstants.Memberships.Assign);

        group.MapPost("subscriptions/{id:guid}/renew", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new RenewMembershipCommand(id))))
        .WithName("AdminRenewMembership")
        .RequirePermission(PermissionConstants.Memberships.Renew);

        group.MapPost("subscriptions/{id:guid}/upgrade", async Task<IResult> (
            Guid id,
            [FromBody] ChangeMembershipPlanRequest request,
            ISender sender) =>
            MapResult(await sender.Send(new UpgradeMembershipCommand(id, request))))
        .WithName("AdminUpgradeMembership")
        .RequirePermission(PermissionConstants.Memberships.Edit);

        group.MapPost("subscriptions/{id:guid}/downgrade", async Task<IResult> (
            Guid id,
            [FromBody] ChangeMembershipPlanRequest request,
            ISender sender) =>
            MapResult(await sender.Send(new DowngradeMembershipCommand(id, request))))
        .WithName("AdminDowngradeMembership")
        .RequirePermission(PermissionConstants.Memberships.Edit);

        group.MapPost("subscriptions/{id:guid}/cancel", async Task<IResult> (Guid id, ISender sender) =>
            MapResult(await sender.Send(new CancelMembershipCommand(id)), StatusCodes.Status204NoContent))
        .WithName("AdminCancelMembership")
        .RequirePermission(PermissionConstants.Memberships.Cancel);
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

internal sealed record DuplicateMembershipPlanRequest(string? NewName, string? NewSlug);
