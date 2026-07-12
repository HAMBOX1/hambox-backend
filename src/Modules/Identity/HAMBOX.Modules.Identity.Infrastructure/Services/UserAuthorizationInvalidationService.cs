using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class UserAuthorizationInvalidationService(
    IdentityDbContext dbContext,
    IPermissionResolver permissionResolver) : IUserAuthorizationInvalidationService
{
    public async Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return;
        }

        user.RotateSecurityStamp();
        await dbContext.SaveChangesAsync(cancellationToken);
        permissionResolver.Invalidate(userId);
    }

    public async Task InvalidateUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var users = await dbContext.Users
            .Where(u => ids.Contains(u.Id))
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            user.RotateSecurityStamp();
            permissionResolver.Invalidate(user.Id);
        }

        if (users.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task InvalidateUsersInRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var userIds = await dbContext.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        await InvalidateUsersAsync(userIds, cancellationToken);
    }
}
