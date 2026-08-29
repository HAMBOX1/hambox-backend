namespace HAMBOX.Modules.Identity.Domain.Enums;

/// <summary>
/// The lifecycle status of the underlying token/code at the moment a
/// <see cref="Audit.CustomerOtpAuditLog"/> event was recorded.
/// </summary>
public enum CustomerOtpEventStatus
{
    /// <summary>A new token/code was issued and is awaiting use.</summary>
    Pending = 0,

    /// <summary>The token/code was successfully consumed.</summary>
    Used = 1,

    /// <summary>A verification attempt was made after the token/code had expired.</summary>
    Expired = 2,

    /// <summary>A verification attempt failed (wrong/unknown token or code).</summary>
    Failed = 3,

    /// <summary>The token/code was superseded before use (e.g. a resend issued a new one).</summary>
    Invalidated = 4
}
