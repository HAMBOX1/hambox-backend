using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Roles;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Roles;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.SetRolePermissions;

internal sealed class SetRolePermissionsCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IRbacAuthorizationService rbacAuthorizationService,
    IPermissionResolver permissionResolver,
    IAuthorizationAuditService auditService,
    IUserAuthorizationInvalidationService invalidationService)
    : IRequestHandler<SetRolePermissionsCommand, Result>
{
    public async Task<Result> Handle(SetRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var actorResult = RoleActorContext.GetActorUserId(currentUserService);
        if (actorResult.IsFailure)
        {
            return Result.Failure(actorResult.Error);
        }

        var actorId = actorResult.Value;

        var role = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(IdentityErrors.RoleNotFound);
        }

        if (RoleConstants.IsOwnerRole(role.Name))
        {
            return Result.Success();
        }

        if (!await rbacAuthorizationService.CanManageRoleAsync(actorId, role.Id, cancellationToken))
        {
            return Result.Failure(IdentityErrors.InsufficientPrivileges);
        }

        var distinctPermissionIds = request.PermissionIds.Distinct().ToList();
        if (distinctPermissionIds.Count > 0)
        {
            var validCount = await dbContext.Permissions
                .CountAsync(p => distinctPermissionIds.Contains(p.Id), cancellationToken);

            if (validCount != distinctPermissionIds.Count)
            {
                return Result.Failure(IdentityErrors.PermissionDenied);
            }
        }

        var actorHasRole = await dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == actorId && ur.RoleId == role.Id, cancellationToken);

        if (actorHasRole && !await rbacAuthorizationService.IsOwnerAsync(actorId, cancellationToken))
        {
            var actorPermissions = await permissionResolver.GetPermissionsAsync(actorId, cancellationToken);
            var newPermissionNames = await dbContext.Permissions
                .Where(p => distinctPermissionIds.Contains(p.Id))
                .Select(p => p.Name)
                .ToListAsync(cancellationToken);

            var otherRoleIds = await dbContext.UserRoles
                .Where(ur => ur.UserId == actorId && ur.RoleId != role.Id)
                .Select(ur => ur.RoleId)
                .ToListAsync(cancellationToken);

            var otherPermissionNames = otherRoleIds.Count == 0
                ? []
                : await (
                    from rp in dbContext.RolePermissions
                    join p in dbContext.Permissions on rp.PermissionId equals p.Id
                    where otherRoleIds.Contains(rp.RoleId)
                    select p.Name
                ).Distinct().ToListAsync(cancellationToken);

            var otherPermissionSet = otherPermissionNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var permission in actorPermissions)
            {
                if (otherPermissionSet.Contains(permission))
                {
                    continue;
                }

                if (!newPermissionNames.Contains(permission, StringComparer.OrdinalIgnoreCase))
                {
                    return Result.Failure(IdentityErrors.PermissionDenied);
                }
            }
        }

        var existingPermissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        dbContext.RolePermissions.RemoveRange(existingPermissions);

        foreach (var permissionId in distinctPermissionIds)
        {
            dbContext.RolePermissions.Add(RolePermission.Create(role.Id, permissionId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuthorizationAuditActions.PermissionsChanged,
            "Role",
            role.Id,
            actorId,
            $"Updated permissions for role '{role.Name}'.",
            request.IpAddress,
            cancellationToken);

        await invalidationService.InvalidateUsersInRoleAsync(role.Id, cancellationToken);

        return Result.Success();
    }
}
