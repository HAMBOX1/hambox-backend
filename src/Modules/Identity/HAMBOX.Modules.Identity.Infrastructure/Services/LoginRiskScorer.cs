using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class LoginRiskScorer : ILoginRiskScorer
{
    public SecurityEventSeverity ScoreSuccessfulLogin(bool isNewDevice, bool isNewCountry)
    {
        if (isNewDevice && isNewCountry)
        {
            return SecurityEventSeverity.High;
        }

        return isNewDevice ? SecurityEventSeverity.Medium : SecurityEventSeverity.Low;
    }

    public SecurityEventSeverity ScoreFailedPassword(int accessFailedCount, int maxFailedAccessAttempts)
    {
        return accessFailedCount >= maxFailedAccessAttempts - 1
            ? SecurityEventSeverity.Critical
            : SecurityEventSeverity.Low;
    }
}
