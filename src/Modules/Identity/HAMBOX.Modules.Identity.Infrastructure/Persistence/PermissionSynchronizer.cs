using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Domain.Permissions;
using HAMBOX.Modules.Identity.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Keeps the Owner role's granted permissions in lockstep with <see cref="PermissionConstants.All"/>
/// every time the app boots — in every environment, not just where migrations auto-apply. Owner bypasses
/// authorization checks by role name (see <see cref="RoleConstants.IsOwnerRole"/> and
/// <c>PermissionAuthorizationHandler</c>/<c>RbacAuthorizationService</c>), so Owner never strictly needs
/// explicit <see cref="Domain.Roles.RolePermission"/> rows to pass a check — but <c>PermissionResolver</c>
/// still returns Owner's *resolved* permission list from <see cref="PermissionConstants.All"/>, and any
/// future code that queries <c>RolePermissions</c> directly (rather than calling the bypass) should see
/// Owner holding every real permission, not zero. This runs idempotently: a permission or group already
/// present is left untouched, so re-running on every startup is a cheap no-op once caught up.
/// </summary>
public static class PermissionSynchronizer
{
    public sealed record SyncReport(
        int TotalRegisteredPermissions,
        int OwnerPermissionsBeforeSync,
        int MissingPermissionsAdded,
        int ObsoletePermissionsRemoved,
        int FinalOwnerPermissionCount);

    public static async Task<SyncReport> SyncAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(PermissionSynchronizer));

        var registeredNames = PermissionConstants.All;

        // 1. Ensure every PermissionGroup the registry knows about exists (matched by unique Key).
        var groupIdByKey = await db.PermissionGroups
            .Select(g => new { g.Key, g.Id })
            .ToDictionaryAsync(g => g.Key, g => g.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var groupDef in PermissionDefinitionRegistry.Groups)
        {
            if (groupIdByKey.ContainsKey(groupDef.Key))
            {
                continue;
            }

            var group = PermissionGroup.Create(groupDef.Key, groupDef.Name, groupDef.Module, groupDef.SortOrder, groupDef.Description);
            db.PermissionGroups.Add(group);
            groupIdByKey[groupDef.Key] = group.Id;
        }

        // 2. Ensure every permission name reflection-discovered on PermissionConstants has a DB row.
        //    Curated metadata (group/description/sort order) comes from PermissionDefinitionRegistry when
        //    available; a permission const added without a matching registry entry still gets synced —
        //    it just falls back to a generic group named after its first name segment, with a log warning
        //    so a developer knows to add proper metadata later. This is what makes new permissions show up
        //    automatically without anyone remembering a manual step.
        var definitionByName = PermissionDefinitionRegistry.Permissions.ToDictionary(p => p.Name, StringComparer.Ordinal);

        var existingPermissions = await db.Permissions.ToListAsync(cancellationToken);
        var permissionIdByName = existingPermissions.ToDictionary(p => p.Name, p => p.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var name in registeredNames)
        {
            if (permissionIdByName.ContainsKey(name))
            {
                continue;
            }

            Guid groupId;
            string? description;
            int sortOrder;

            if (definitionByName.TryGetValue(name, out var definition))
            {
                var groupKey = PermissionDefinitionRegistry.Groups.First(g => g.Id == definition.GroupId).Key;
                groupId = groupIdByKey[groupKey];
                description = definition.Description;
                sortOrder = definition.SortOrder;
            }
            else
            {
                var fallbackKey = name.Split('.')[0];
                if (!groupIdByKey.TryGetValue(fallbackKey, out groupId))
                {
                    var fallbackGroup = PermissionGroup.Create(fallbackKey, fallbackKey, fallbackKey, 999,
                        "Auto-created by permission sync — add a proper entry to PermissionDefinitionRegistry.");
                    db.PermissionGroups.Add(fallbackGroup);
                    groupIdByKey[fallbackKey] = fallbackGroup.Id;
                    groupId = fallbackGroup.Id;
                }

                description = null;
                sortOrder = 0;
                logger.LogWarning(
                    "Permission {PermissionName} exists in PermissionConstants but has no PermissionDefinitionRegistry entry; " +
                    "synced with fallback group {FallbackGroup}. Add it to PermissionDefinitionRegistry for proper grouping.",
                    name, fallbackKey);
            }

            var permission = Permission.Create(groupId, name, description, sortOrder);
            db.Permissions.Add(permission);
            permissionIdByName[name] = permission.Id;
        }

        await db.SaveChangesAsync(cancellationToken);

        // 3. Sync the Owner role's RolePermission rows to exactly the current registered-permission set.
        //    Mutated directly via the RolePermissions DbSet (not ApplicationRole.AddPermission/RemovePermission)
        //    so this is a plain set of Added/Deleted child rows — no aggregate-collection navigation fixup
        //    to reason about, and no risk of the Owner role row itself getting swept into the same save.
        var ownerRoleId = await db.Roles
            .Where(r => r.NormalizedName == RoleConstants.Owner.ToUpperInvariant())
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerRoleId is null)
        {
            logger.LogWarning("Permission sync skipped: no Owner role found (Identity schema not seeded yet).");
            return new SyncReport(registeredNames.Count, 0, 0, 0, 0);
        }

        var existingOwnerRolePermissions = await db.RolePermissions
            .Where(rp => rp.RoleId == ownerRoleId.Value)
            .ToListAsync(cancellationToken);

        var ownerPermissionsBefore = existingOwnerRolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        var targetPermissionIds = registeredNames.Select(name => permissionIdByName[name]).ToHashSet();

        var missingIds = targetPermissionIds.Except(ownerPermissionsBefore).ToList();
        var obsoleteIds = ownerPermissionsBefore.Except(targetPermissionIds).ToList();

        foreach (var id in missingIds)
        {
            db.RolePermissions.Add(RolePermission.Create(ownerRoleId.Value, id));
        }

        if (obsoleteIds.Count > 0)
        {
            db.RolePermissions.RemoveRange(
                existingOwnerRolePermissions.Where(rp => obsoleteIds.Contains(rp.PermissionId)));
        }

        await db.SaveChangesAsync(cancellationToken);

        var report = new SyncReport(
            TotalRegisteredPermissions: registeredNames.Count,
            OwnerPermissionsBeforeSync: ownerPermissionsBefore.Count,
            MissingPermissionsAdded: missingIds.Count,
            ObsoletePermissionsRemoved: obsoleteIds.Count,
            FinalOwnerPermissionCount: targetPermissionIds.Count);

        logger.LogInformation(
            "Permission sync complete. Registered={Total} OwnerBefore={Before} Added={Added} Removed={Removed} OwnerAfter={After}",
            report.TotalRegisteredPermissions, report.OwnerPermissionsBeforeSync,
            report.MissingPermissionsAdded, report.ObsoletePermissionsRemoved, report.FinalOwnerPermissionCount);

        return report;
    }
}
