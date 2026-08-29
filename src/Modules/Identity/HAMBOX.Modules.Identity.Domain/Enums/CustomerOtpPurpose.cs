namespace HAMBOX.Modules.Identity.Domain.Enums;

/// <summary>
/// The customer-facing verification/reset flow a <see cref="Audit.CustomerOtpAuditLog"/> entry belongs to.
/// </summary>
public enum CustomerOtpPurpose
{
    EmailVerification = 0,
    PasswordReset = 1
}
