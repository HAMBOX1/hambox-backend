using Asp.Versioning.Builder;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Roles.AssignUsersToRole;
using HAMBOX.Modules.Identity.Application.Features.Roles.CreateRole;
using HAMBOX.Modules.Identity.Application.Features.Roles.DeleteRole;
using HAMBOX.Modules.Identity.Application.Features.Roles.DuplicateRole;
using HAMBOX.Modules.Identity.Application.Features.Roles.GetPermissionMatrix;
using HAMBOX.Modules.Identity.Application.Features.Roles.GetRoleById;
using HAMBOX.Modules.Identity.Application.Features.Roles.GetRoles;
using HAMBOX.Modules.Identity.Application.Features.Roles.GetRoleUsers;
using HAMBOX.Modules.Identity.Application.Features.Roles.RemoveUserFromRole;
using HAMBOX.Modules.Identity.Application.Features.Roles.SearchUsers;
using HAMBOX.Modules.Identity.Application.Features.Roles.SetRolePermissions;
using HAMBOX.Modules.Identity.Application.Features.Roles.UpdateRole;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Identity.Presentation.Endpoints;

/// <summary>
/// Registers role management endpoints.
/// </summary>
internal static class RoleEndpoints
{
    public static void MapRoleEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/roles")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Roles")
            .HasApiVersion(1)
            .RequireAuthorization();

        group.MapGet("", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var result = await sender.Send(new GetRolesQuery(pageNumber, pageSize, searchTerm));
            return MapResult(result);
        })
        .WithName("GetRoles")
        .RequirePermission(PermissionConstants.Roles.View);

        group.MapGet("permissions/matrix", async Task<IResult> (ISender sender) =>
        {
            var result = await sender.Send(new GetPermissionMatrixQuery());
            return MapResult(result);
        })
        .WithName("GetPermissionMatrix")
        .RequireAnyPermission(
            PermissionConstants.Permissions.View,
            PermissionConstants.Roles.View);

        group.MapGet("users/search", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] Guid? excludeRoleId,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var result = await sender.Send(new SearchUsersQuery(pageNumber, pageSize, searchTerm, excludeRoleId));
            return MapResult(result);
        })
        .WithName("SearchUsersForRoleAssignment")
        .RequirePermission(PermissionConstants.Users.View);

        group.MapGet("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetRoleByIdQuery(id));
            return MapResult(result);
        })
        .WithName("GetRoleById")
        .RequirePermission(PermissionConstants.Roles.View);

        group.MapGet("{id:guid}/users", async Task<IResult> (
            Guid id,
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            ISender sender) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var result = await sender.Send(new GetRoleUsersQuery(id, pageNumber, pageSize, searchTerm));
            return MapResult(result);
        })
        .WithName("GetRoleUsers")
        .RequirePermission(PermissionConstants.Roles.View);

        group.MapPost("", async Task<IResult> (
            [FromBody] CreateRoleRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateRoleCommand(
                request.Name,
                request.Description,
                request.PriorityLevel,
                GetIpAddress(httpContext));

            var result = await sender.Send(command, ct);
            return MapCreatedResult(result);
        })
        .WithName("CreateRole")
        .RequirePermission(PermissionConstants.Roles.Create);

        group.MapPut("{id:guid}", async Task<IResult> (
            Guid id,
            [FromBody] UpdateRoleRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new UpdateRoleCommand(
                id,
                request.Name,
                request.Description,
                request.PriorityLevel,
                request.IsDefault,
                GetIpAddress(httpContext));

            var result = await sender.Send(command, ct);
            return MapResult(result);
        })
        .WithName("UpdateRole")
        .RequirePermission(PermissionConstants.Roles.Edit);

        group.MapDelete("{id:guid}", async Task<IResult> (
            Guid id,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteRoleCommand(id, GetIpAddress(httpContext)), ct);
            return MapResult(result);
        })
        .WithName("DeleteRole")
        .RequirePermission(PermissionConstants.Roles.Delete);

        group.MapPost("{id:guid}/duplicate", async Task<IResult> (
            Guid id,
            [FromBody] DuplicateRoleRequest? request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new DuplicateRoleCommand(id, request?.NewName, GetIpAddress(httpContext));
            var result = await sender.Send(command, ct);
            return MapCreatedResult(result);
        })
        .WithName("DuplicateRole")
        .RequirePermission(PermissionConstants.Roles.Create);

        group.MapPut("{id:guid}/permissions", async Task<IResult> (
            Guid id,
            [FromBody] SetRolePermissionsRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new SetRolePermissionsCommand(
                id,
                request.PermissionIds,
                GetIpAddress(httpContext));

            var result = await sender.Send(command, ct);
            return MapResult(result);
        })
        .WithName("SetRolePermissions")
        .RequirePermission(PermissionConstants.Roles.Edit);

        group.MapPost("{id:guid}/users", async Task<IResult> (
            Guid id,
            [FromBody] AssignUsersRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new AssignUsersToRoleCommand(id, request.UserIds, GetIpAddress(httpContext));
            var result = await sender.Send(command, ct);
            return MapResult(result);
        })
        .WithName("AssignUsersToRole")
        .RequirePermission(PermissionConstants.Roles.AssignUsers);

        group.MapDelete("{id:guid}/users/{userId:guid}", async Task<IResult> (
            Guid id,
            Guid userId,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new RemoveUserFromRoleCommand(id, userId, GetIpAddress(httpContext));
            var result = await sender.Send(command, ct);
            return MapResult(result);
        })
        .WithName("RemoveUserFromRole")
        .RequirePermission(PermissionConstants.Roles.AssignUsers);
    }

    private static string? GetIpAddress(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString();

    private static IResult MapResult(Result result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.NoContent();
        }

        return MapFailure(result.Error);
    }

    private static IResult MapResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return MapFailure(result.Error);
    }

    private static IResult MapCreatedResult(Result<Guid> result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Created($"/api/v1/roles/{result.Value}", result.Value);
        }

        return MapFailure(result.Error);
    }

    private static IResult MapFailure(Error error)
    {
        var problem = new ProblemDetails
        {
            Title = GetTitle(error),
            Detail = error.Description,
            Type = error.Code,
            Status = GetStatusCode(error),
        };

        return TypedResults.Problem(problem);
    }

    private static int GetStatusCode(Error error) => error.Code switch
    {
        _ when error == IdentityErrors.RoleNotFound || error == IdentityErrors.UserNotFound => StatusCodes.Status404NotFound,
        _ when error == IdentityErrors.InsufficientPrivileges ||
               error == IdentityErrors.PermissionDenied ||
               error == IdentityErrors.CannotModifyOwnerRole ||
               error == IdentityErrors.CannotDeleteSystemRole => StatusCodes.Status403Forbidden,
        _ when error == IdentityErrors.AuthenticationRequired => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string GetTitle(Error error) => GetStatusCode(error) switch
    {
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        _ => "Bad Request",
    };
}

internal sealed record DuplicateRoleRequest(string? NewName);
