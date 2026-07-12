using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Roles;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.AssignUsersToRole;

internal sealed class AssignUsersToRoleCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IRbacAuthorizationService rbacAuthorizationService,
    IAuthorizationAuditService auditService,
    IUserAuthorizationInvalidationService invalidationService)
    : IRequestHandler<AssignUsersToRoleCommand, Result>
{
    public async Task<Result> Handle(AssignUsersToRoleCommand request, CancellationToken cancellationToken)
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

        var distinctUserIds = request.UserIds.Distinct().ToList();
        var existingUsers = await dbContext.Users
            .Where(u => distinctUserIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (existingUsers.Count != distinctUserIds.Count)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        foreach (var userId in distinctUserIds)
        {
            if (!await rbacAuthorizationService.CanModifyUserRolesAsync(actorId, userId, cancellationToken))
            {
                return Result.Failure(IdentityErrors.InsufficientPrivileges);
            }
        }

        var alreadyAssigned = await dbContext.UserRoles
            .Where(ur => ur.RoleId == role.Id && distinctUserIds.Contains(ur.UserId))
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        var usersToAssign = distinctUserIds.Except(alreadyAssigned).ToList();
        foreach (var userId in usersToAssign)
        {
            dbContext.UserRoles.Add(UserRole.Create(userId, role.Id));
        }

        if (usersToAssign.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var userId in usersToAssign)
            {
                await auditService.RecordAsync(
                    AuthorizationAuditActions.UserAssigned,
                    "Role",
                    role.Id,
                    actorId,
                    $"Assigned user '{userId}' to role '{role.Name}'.",
                    request.IpAddress,
                    cancellationToken);
            }

            await invalidationService.InvalidateUsersAsync(usersToAssign, cancellationToken);
        }

        return Result.Success();
    }
}
