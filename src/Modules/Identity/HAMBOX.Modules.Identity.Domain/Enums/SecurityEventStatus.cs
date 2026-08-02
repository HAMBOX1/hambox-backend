namespace HAMBOX.Modules.Identity.Domain.Enums;

/// <summary>
/// The investigation workflow state of a <see cref="Security.SecurityEventLog"/> entry.
/// </summary>
public enum SecurityEventStatus
{
    Open = 0,
    Acknowledged = 1,
    Dismissed = 2,
    Resolved = 3
}
