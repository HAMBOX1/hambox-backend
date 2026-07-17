using HAMBOX.Modules.Communication.Domain.Communication;

namespace HAMBOX.Modules.Communication.Application.Abstractions;

public sealed record CommunicationPreferencesDto(
    bool EmailEnabled,
    bool InAppEnabled,
    bool MarketingEnabled,
    bool SecurityEnabled,
    bool OrderEnabled,
    bool MembershipEnabled,
    bool SupportEnabled,
    bool PromotionEnabled,
    bool GeneralEnabled);

/// <summary>
/// Reads/writes per-user notification preferences. Reads fall back to all-enabled defaults when no row
/// exists yet — the same "missing row = defaults" philosophy as Platform Settings.
/// </summary>
public interface ICommunicationPreferenceService
{
    Task<CommunicationPreferencesDto> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(string userId, CommunicationPreferencesDto preferences, CancellationToken cancellationToken = default);

    /// <summary>Used by <c>ICommunicationService</c> to decide whether a channel/category should be delivered.</summary>
    Task<bool> IsChannelEnabledAsync(string userId, string channel, CommunicationCategory category, CancellationToken cancellationToken = default);
}
