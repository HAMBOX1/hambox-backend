using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class AdminAccessResolver(
    IdentityDbContext dbContext,
    IPermissionResolver permissionResolver) : IAdminAccessResolver
{
    public async Task<bool> HasAdminPortalAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var roleNames = await (
            from ur in dbContext.UserRoles
            join r in dbContext.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId
            select r.Name
        ).ToListAsync(cancellationToken);

        if (roleNames.Any(RoleConstants.IsOwnerRole))
        {
            return true;
        }

        var permissions = await permissionResolver.GetPermissionsAsync(userId, cancellationToken);
        return permissions.Any(AdminAccessPrefixes.IsAdminAccessPermission);
    }
}
