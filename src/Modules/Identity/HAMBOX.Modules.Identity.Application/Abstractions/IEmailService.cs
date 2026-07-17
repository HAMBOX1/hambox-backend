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

    /// <summary>
    /// Sends an admin portal login OTP code to the specified address.
    /// </summary>
    Task SendAdminLoginOtpAsync(
        Guid userId,
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a pre-rendered transactional email with an arbitrary subject/HTML body. Used by the
    /// Communication Platform's <c>EmailCommunicationProvider</c> so every channel — not just the three
    /// hardcoded identity flows above — reuses this same MailKit/Platform-Settings-driven send path.
    /// </summary>
    Task SendTemplatedEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
