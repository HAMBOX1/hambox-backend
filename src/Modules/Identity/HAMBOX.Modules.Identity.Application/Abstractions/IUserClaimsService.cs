using System.Security.Claims;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Defines the contract for loading user-specific roles and permissions as claims.
/// </summary>
public interface IUserClaimsService
{
    /// <summary>
    /// Retrieves a collection of security claims representing roles and permissions for a user.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of claim objects containing roles and permissions.</returns>
    Task<IReadOnlyCollection<Claim>> GetClaimsAsync(Guid userId, CancellationToken cancellationToken = default);
}
