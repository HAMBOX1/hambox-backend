namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Sends transactional emails for identity operations.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email verification message to the specified address.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="email">The recipient's email address.</param>
    /// <param name="expiresAt">The verification token expiration time in UTC.</param>
    /// <param name="token">The email verification token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendEmailVerificationAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a password reset message to the specified address.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="email">The recipient's email address.</param>
    /// <param name="expiresAt">The reset token expiration time in UTC.</param>
    /// <param name="token">The password reset token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendPasswordResetAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default);
}
