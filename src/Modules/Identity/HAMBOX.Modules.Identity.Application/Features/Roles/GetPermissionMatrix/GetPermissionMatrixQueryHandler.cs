using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Roles;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetPermissionMatrix;

internal sealed class GetPermissionMatrixQueryHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IPermissionResolver permissionResolver)
    : IRequestHandler<GetPermissionMatrixQuery, Result<IReadOnlyCollection<PermissionGroupDto>>>
{
    public async Task<Result<IReadOnlyCollection<PermissionGroupDto>>> Handle(
        GetPermissionMatrixQuery request,
        CancellationToken cancellationToken)
    {
        var actorResult = RoleActorContext.GetActorUserId(currentUserService);
        if (actorResult.IsFailure)
        {
            return Result.Failure<IReadOnlyCollection<PermissionGroupDto>>(actorResult.Error);
        }

        var actorPermissions = await permissionResolver.GetPermissionsAsync(actorResult.Value, cancellationToken);
        if (!actorPermissions.Contains(PermissionConstants.Permissions.View) &&
            !actorPermissions.Contains(PermissionConstants.Roles.View))
        {
            return Result.Failure<IReadOnlyCollection<PermissionGroupDto>>(IdentityErrors.PermissionDenied);
        }

        var groups = await dbContext.PermissionGroups
            .AsNoTracking()
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var permissions = await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var matrix = groups.Select(group => new PermissionGroupDto(
            group.Id,
            group.Key,
            group.Name,
            group.Module,
            group.SortOrder,
            group.Description,
            permissions
                .Where(p => p.GroupId == group.Id)
                .Select(p => new PermissionDto(p.Id, p.Name, p.Description, p.SortOrder))
                .ToList()))
            .ToList();

        return Result.Success<IReadOnlyCollection<PermissionGroupDto>>(matrix);
    }
}
