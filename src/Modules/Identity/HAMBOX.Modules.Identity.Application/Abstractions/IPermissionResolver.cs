namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Resolves effective permissions for a user with optional caching.
/// </summary>
public interface IPermissionResolver
{
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> GetHighestPrivilegeLevelAsync(Guid userId, CancellationToken cancellationToken = default);

    void Invalidate(Guid userId);
}
