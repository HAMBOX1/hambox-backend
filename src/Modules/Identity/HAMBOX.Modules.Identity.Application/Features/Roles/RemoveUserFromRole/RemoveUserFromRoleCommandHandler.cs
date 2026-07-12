using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Roles;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.RemoveUserFromRole;

internal sealed class RemoveUserFromRoleCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IRbacAuthorizationService rbacAuthorizationService,
    IAuthorizationAuditService auditService,
    IUserAuthorizationInvalidationService invalidationService)
    : IRequestHandler<RemoveUserFromRoleCommand, Result>
{
    public async Task<Result> Handle(RemoveUserFromRoleCommand request, CancellationToken cancellationToken)
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

        if (!await rbacAuthorizationService.CanAssignRoleAsync(actorId, role.Id, cancellationToken))
        {
            return Result.Failure(IdentityErrors.InsufficientPrivileges);
        }

        if (!await rbacAuthorizationService.CanModifyUserRolesAsync(actorId, request.UserId, cancellationToken))
        {
            return Result.Failure(IdentityErrors.InsufficientPrivileges);
        }

        var assignment = await dbContext.UserRoles
            .FirstOrDefaultAsync(
                ur => ur.RoleId == request.RoleId && ur.UserId == request.UserId,
                cancellationToken);

        if (assignment is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        if (RoleConstants.IsOwnerRole(role.Name) && request.UserId == actorId)
        {
            var ownerUserCount = await (
                from ur in dbContext.UserRoles
                join r in dbContext.Roles on ur.RoleId equals r.Id
                where r.Name == RoleConstants.Owner || r.Name == RoleConstants.SuperAdmin
                select ur.UserId
            ).Distinct().CountAsync(cancellationToken);

            if (ownerUserCount <= 1)
            {
                return Result.Failure(IdentityErrors.PermissionDenied);
            }
        }

        dbContext.UserRoles.Remove(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuthorizationAuditActions.UserRemoved,
            "Role",
            role.Id,
            actorId,
            $"Removed user '{request.UserId}' from role '{role.Name}'.",
            request.IpAddress,
            cancellationToken);

        await invalidationService.InvalidateUserAsync(request.UserId, cancellationToken);

        return Result.Success();
    }
}
