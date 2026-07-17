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
    AdminBlock = 9
}
