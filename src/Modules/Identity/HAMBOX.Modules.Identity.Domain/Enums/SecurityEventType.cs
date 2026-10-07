namespace HAMBOX.Modules.Identity.Domain.Enums;

/// <summary>
/// Categorizes the kind of security-relevant occurrence recorded in a security event log entry.
/// </summary>
public enum SecurityEventType
{
    FailedLogin = 0,
    BlockedLogin = 1,
    CountryBlock = 2,
    IpBlock = 3,
    EmailBlock = 4,
    ManualSuspension = 5,
    ManualBan = 6,
    PermissionDenied = 7,
    AdminUnlock = 8,
    AdminBlock = 9,
    DeviceBlock = 10,

    /// <summary>A customer-facing OTP/verification-token event worth surfacing (a failed/expired
    /// verification attempt) — see <see cref="HAMBOX.Modules.Identity.Domain.Audit.CustomerOtpAuditLog"/>
    /// for the full lifecycle detail.</summary>
    CustomerOtpEvent = 11,

    /// <summary>A user ended their own session normally via the logout endpoint.</summary>
    Logout = 12,

    /// <summary>The backend ended a session on its own authority — idle timeout or absolute max
    /// session lifetime exceeded — rather than the user or an admin explicitly ending it.</summary>
    SessionExpired = 13,

    /// <summary>A single session was force-ended by someone other than its own owner (an admin
    /// revoking one of a target user's sessions from the Security Center), or by reuse-detection's
    /// cascade revoke. Not used for the owner's own logout (see <see cref="Logout"/>) or their own
    /// idle/expiry (see <see cref="SessionExpired"/>).</summary>
    SessionRevoked = 14,

    /// <summary>Every session for a user was revoked in one action — either self-service ("sign out
    /// all other sessions", which preserves the caller's own session) or an admin revoking all of a
    /// target user's sessions.</summary>
    SignOutAllDevices = 15
}
