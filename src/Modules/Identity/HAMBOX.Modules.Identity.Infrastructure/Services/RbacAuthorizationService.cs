using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Enforces RBAC business rules: privilege levels, Owner protection, escalation prevention.
/// </summary>
internal sealed class RbacAuthorizationService(
    IdentityDbContext dbContext,
    IPermissionResolver permissionResolver) : IRbacAuthorizationService
{
    public async Task<bool> IsOwnerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roles = await GetUserRoleNamesAsync(userId, cancellationToken);
        return roles.Any(RoleConstants.IsOwnerRole);
    }

    public async Task<bool> CanManageRoleAsync(
        Guid actorUserId,
        Guid targetRoleId,
        CancellationToken cancellationToken = default)
    {
        if (await IsOwnerAsync(actorUserId, cancellationToken))
        {
            return true;
        }

        var targetRole = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == targetRoleId, cancellationToken);

        if (targetRole is null)
        {
            return false;
        }

        if (RoleConstants.IsOwnerRole(targetRole.Name))
        {
            return false;
        }

        var actorLevel = await permissionResolver.GetHighestPrivilegeLevelAsync(actorUserId, cancellationToken);
        return actorLevel < targetRole.PriorityLevel;
    }

    public async Task<bool> CanAssignRoleAsync(
        Guid actorUserId,
        Guid roleIdToAssign,
        CancellationToken cancellationToken = default)
    {
        return await CanManageRoleAsync(actorUserId, roleIdToAssign, cancellationToken);
    }

    public async Task<bool> CanModifyUserRolesAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == targetUserId)
        {
            var actorRoles = await GetUserRoleNamesAsync(actorUserId, cancellationToken);
            if (actorRoles.Any(RoleConstants.IsOwnerRole))
            {
                return false;
            }
        }

        if (await IsOwnerAsync(actorUserId, cancellationToken))
        {
            return true;
        }

        var targetIsOwner = await IsOwnerAsync(targetUserId, cancellationToken);
        if (targetIsOwner)
        {
            return false;
        }

        var actorLevel = await permissionResolver.GetHighestPrivilegeLevelAsync(actorUserId, cancellationToken);
        var targetLevel = await permissionResolver.GetHighestPrivilegeLevelAsync(targetUserId, cancellationToken);
        return actorLevel < targetLevel;
    }

    public Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    private async Task<IReadOnlyCollection<string>> GetUserRoleNamesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await (
            from ur in dbContext.UserRoles
            join r in dbContext.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId
            select r.Name
        ).ToListAsync(cancellationToken);
    }
}
