namespace HAMBOX.Modules.Identity.Domain.Enums;

/// <summary>
/// Indicates how serious a recorded security event is, for triage and dashboard display.
/// </summary>
public enum SecurityEventSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
