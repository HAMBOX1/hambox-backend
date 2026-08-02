using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class SecurityAlertRecipientResolver(IdentityDbContext dbContext) : ISecurityAlertRecipientResolver
{
    private static readonly string[] AlertPermissions =
    [
        PermissionConstants.Security.ManageAlerts,
        PermissionConstants.Security.View,
    ];

    public async Task<IReadOnlyCollection<Guid>> GetRecipientUserIdsAsync(CancellationToken cancellationToken = default)
    {
        var roleNames = await dbContext.Roles.Select(r => new { r.Id, r.Name }).ToListAsync(cancellationToken);
        var ownerRoleIds = roleNames.Where(r => RoleConstants.IsOwnerRole(r.Name)).Select(r => r.Id).ToList();

        var permissionRoleIds = await (
            from rp in dbContext.RolePermissions
            join p in dbContext.Permissions on rp.PermissionId equals p.Id
            where AlertPermissions.Contains(p.Name)
            select rp.RoleId
        ).Distinct().ToListAsync(cancellationToken);

        var recipientRoleIds = ownerRoleIds.Concat(permissionRoleIds).Distinct().ToList();

        var userIds = await dbContext.UserRoles
            .Where(ur => recipientRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return userIds;
    }
}
