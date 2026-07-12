using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class UserClaimsService(
    IdentityDbContext dbContext,
    IPermissionResolver permissionResolver) : IUserClaimsService
{
    public async Task<IReadOnlyCollection<Claim>> GetClaimsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var roleNames = await (
            from ur in dbContext.UserRoles
            join r in dbContext.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId
            select r.Name
        ).ToListAsync(cancellationToken);

        if (roleNames.Count == 0)
        {
            return Array.Empty<Claim>();
        }

        var claims = new List<Claim>();
        foreach (var roleName in roleNames)
        {
            claims.Add(new Claim(IdentityClaimTypes.Role, roleName));
        }

        var permissions = await permissionResolver.GetPermissionsAsync(userId, cancellationToken);
        foreach (var permission in permissions)
        {
            claims.Add(new Claim(IdentityClaimTypes.Permission, permission));
        }

        return claims;
    }
}
