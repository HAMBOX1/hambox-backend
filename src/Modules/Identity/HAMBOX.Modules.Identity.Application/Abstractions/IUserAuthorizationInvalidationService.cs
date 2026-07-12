namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Invalidates cached permissions and rotates security stamps when role assignments change.
/// </summary>
public interface IUserAuthorizationInvalidationService
{
    Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task InvalidateUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    Task InvalidateUsersInRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}
