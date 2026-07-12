using System.Security.Claims;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// RBAC authorization guard for role management operations.
/// </summary>
public interface IRbacAuthorizationService
{
    Task<bool> IsOwnerAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> CanManageRoleAsync(Guid actorUserId, Guid targetRoleId, CancellationToken cancellationToken = default);

    Task<bool> CanAssignRoleAsync(Guid actorUserId, Guid roleIdToAssign, CancellationToken cancellationToken = default);

    Task<bool> CanModifyUserRolesAsync(Guid actorUserId, Guid targetUserId, CancellationToken cancellationToken = default);

    Guid? GetCurrentUserId(ClaimsPrincipal user);
}
