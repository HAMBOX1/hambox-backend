namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Validates that a JWT security stamp claim still matches the persisted user record.
/// </summary>
public interface ISecurityStampValidator
{
    /// <summary>
    /// Determines whether the supplied security stamp is still valid for the user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="securityStamp">The security stamp from the access token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the stamp matches an active user; otherwise <see langword="false"/>.</returns>
    Task<bool> ValidateAsync(Guid userId, string securityStamp, CancellationToken cancellationToken = default);
}
