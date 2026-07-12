namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Determines whether a user may access the admin portal.
/// </summary>
public interface IAdminAccessResolver
{
    /// <summary>
    /// Returns true when the user has Owner role or any admin-area permission.
    /// </summary>
    Task<bool> HasAdminPortalAccessAsync(Guid userId, CancellationToken cancellationToken = default);
}
