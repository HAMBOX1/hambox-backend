using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Roles;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.DeleteRole;

internal sealed class DeleteRoleCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IRbacAuthorizationService rbacAuthorizationService,
    IAuthorizationAuditService auditService,
    IUserAuthorizationInvalidationService invalidationService)
    : IRequestHandler<DeleteRoleCommand, Result>
{
    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var actorResult = RoleActorContext.GetActorUserId(currentUserService);
        if (actorResult.IsFailure)
        {
            return Result.Failure(actorResult.Error);
        }

        var role = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(IdentityErrors.RoleNotFound);
        }

        if (role.IsSystem)
        {
            return Result.Failure(IdentityErrors.CannotDeleteSystemRole);
        }

        if (!await rbacAuthorizationService.CanManageRoleAsync(actorResult.Value, role.Id, cancellationToken))
        {
            return Result.Failure(IdentityErrors.InsufficientPrivileges);
        }

        var affectedUserIds = await dbContext.UserRoles
            .Where(ur => ur.RoleId == role.Id)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        var roleName = role.Name;
        var roleId = role.Id;

        dbContext.Roles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuthorizationAuditActions.RoleDeleted,
            "Role",
            roleId,
            actorResult.Value,
            $"Deleted role '{roleName}'.",
            request.IpAddress,
            cancellationToken);

        await invalidationService.InvalidateUsersAsync(affectedUserIds, cancellationToken);

        return Result.Success();
    }
}
