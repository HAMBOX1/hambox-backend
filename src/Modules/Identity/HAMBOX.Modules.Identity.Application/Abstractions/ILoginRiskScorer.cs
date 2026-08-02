using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Computes a deterministic risk level for a login attempt. Deliberately simple, rule-based
/// signals only (no ML) — reused identically by both the customer and admin login handlers so
/// the two flows can't drift apart.
/// </summary>
public interface ILoginRiskScorer
{
    /// <summary>
    /// Scores a successful login based on whether the device and/or country are new for this
    /// user, reusing <see cref="SecurityEventSeverity"/> as the risk scale.
    /// </summary>
    SecurityEventSeverity ScoreSuccessfulLogin(bool isNewDevice, bool isNewCountry);

    /// <summary>
    /// Scores an ordinary failed-password attempt, escalating to <see cref="SecurityEventSeverity.Critical"/>
    /// when the account is one attempt away from lockout (reuses the existing failed-attempt
    /// counter rather than a new velocity check).
    /// </summary>
    SecurityEventSeverity ScoreFailedPassword(int accessFailedCount, int maxFailedAccessAttempts);
}
