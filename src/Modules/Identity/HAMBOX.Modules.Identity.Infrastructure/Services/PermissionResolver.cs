using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class PermissionResolver(
    IdentityDbContext dbContext,
    IMemoryCache memoryCache) : IPermissionResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await memoryCache.GetOrCreateAsync(GetCacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await LoadPermissionsFromDbAsync(userId, cancellationToken);
        }) ?? Array.Empty<string>();
    }

    public async Task<int> GetHighestPrivilegeLevelAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var roleIds = await dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return int.MaxValue;
        }

        var minLevel = await dbContext.Roles
            .Where(r => roleIds.Contains(r.Id))
            .MinAsync(r => r.PriorityLevel, cancellationToken);

        return minLevel;
    }

    public void Invalidate(Guid userId) => memoryCache.Remove(GetCacheKey(userId));

    private static string GetCacheKey(Guid userId) => $"permissions:{userId}";

    private async Task<IReadOnlyCollection<string>> LoadPermissionsFromDbAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var roleData = await (
            from ur in dbContext.UserRoles
            join r in dbContext.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId
            select new { r.Name, r.PriorityLevel, r.Id }
        ).ToListAsync(cancellationToken);

        if (roleData.Any(r => RoleConstants.IsOwnerRole(r.Name)))
        {
            return PermissionConstants.All.ToArray();
        }

        var permissionIds = await dbContext.RolePermissions
            .Where(rp => roleData.Select(r => r.Id).Contains(rp.RoleId))
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (permissionIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        return await dbContext.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}
