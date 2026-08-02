using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Application.Features.Security.SecurityEvents;

/// <summary>
/// <see cref="SecurityEventSeverity"/> is persisted as a string (see <c>SecurityEventLogConfiguration</c>),
/// so a SQL <c>&gt;=</c> comparison against it sorts alphabetically, not by enum ordinal — "Low"
/// would incorrectly satisfy "&gt;= High" (L &gt; H). Use <see cref="AtOrAbove"/> with <c>.Contains(...)</c>
/// instead of a direct <c>&gt;=</c> comparison wherever a minimum-severity filter is needed.
/// </summary>
internal static class SecuritySeverityFilter
{
    public static IReadOnlyCollection<SecurityEventSeverity> AtOrAbove(SecurityEventSeverity minimum) =>
        Enum.GetValues<SecurityEventSeverity>().Where(s => s >= minimum).ToArray();
}
