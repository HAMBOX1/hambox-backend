using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Service that loads role and permission claims from the database for a user.
/// </summary>
internal sealed class UserClaimsService(IdentityDbContext dbContext) : IUserClaimsService
{
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Claim>> GetClaimsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch the user's role identifiers
        var roleIds = await dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return Array.Empty<Claim>();
        }

        // 2. Fetch the corresponding roles
        var roles = await dbContext.Roles
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        var claims = new List<Claim>();

        // 3. Add role claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(IdentityClaimTypes.Role, role.Name));
        }

        // 4. Collect and distinct permission identifiers
        var permissionIds = roles
            .SelectMany(r => r.PermissionIds)
            .Distinct()
            .ToList();

        if (permissionIds.Count > 0)
        {
            // 5. Fetch the corresponding permission names
            var permissions = await dbContext.Permissions
                .Where(p => permissionIds.Contains(p.Id))
                .Select(p => p.Name)
                .ToListAsync(cancellationToken);

            // 6. Add permission claims
            foreach (var permission in permissions)
            {
                claims.Add(new Claim(IdentityClaimTypes.Permission, permission));
            }
        }

        return claims;
    }
}
