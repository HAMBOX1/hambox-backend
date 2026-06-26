namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Settings for configuring transactional email delivery.
/// </summary>
public sealed class EmailSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "EmailSettings";

    /// <summary>
    /// Gets a value indicating whether SMTP delivery is enabled.
    /// When false, emails are logged only.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the SMTP server hostname.
    /// </summary>
    public string SmtpHost { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SMTP server port.
    /// </summary>
    public int SmtpPort { get; init; } = 587;

    /// <summary>
    /// Gets a value indicating whether to use TLS when connecting to SMTP.
    /// </summary>
    public bool UseSsl { get; init; }

    /// <summary>
    /// Gets the SMTP authentication username, if required.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the SMTP authentication password, if required.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Gets the sender email address.
    /// </summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>
    /// Gets the sender display name.
    /// </summary>
    public string FromName { get; init; } = "HAMBOX";

    /// <summary>
    /// Gets the public application base URL used to build links in emails.
    /// </summary>
    public string ApplicationBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the frontend path for email verification links.
    /// </summary>
    public string VerificationPath { get; init; } = "/auth/verify-email";

    /// <summary>
    /// Gets the frontend path for password reset links.
    /// </summary>
    public string ResetPasswordPath { get; init; } = "/auth/reset-password";
}
