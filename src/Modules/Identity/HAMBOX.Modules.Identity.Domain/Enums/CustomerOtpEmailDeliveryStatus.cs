namespace HAMBOX.Modules.Identity.Domain.Enums;

/// <summary>
/// The outcome of attempting to email the customer for a <see cref="Audit.CustomerOtpAuditLog"/> event.
/// </summary>
public enum CustomerOtpEmailDeliveryStatus
{
    /// <summary>No email was attempted for this event (e.g. a failed/expired verification attempt).</summary>
    NotApplicable = 0,

    /// <summary>The configured email provider reported a successful send.</summary>
    Sent = 1,

    /// <summary>The configured email provider reported a failed send.</summary>
    Failed = 2
}
