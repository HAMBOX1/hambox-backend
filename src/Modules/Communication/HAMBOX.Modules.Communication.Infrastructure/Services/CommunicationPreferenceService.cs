using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Infrastructure.Services;

/// <summary>
/// Lazily creates a <see cref="CommunicationPreference"/> row on first write; reads fall back to
/// all-enabled defaults when no row exists yet — the same "missing row = defaults" philosophy as
/// Platform Settings, so no backfill migration is needed for existing users.
/// </summary>
internal sealed class CommunicationPreferenceService(ICommunicationDbContext db) : ICommunicationPreferenceService
{
    public async Task<CommunicationPreferencesDto> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var preference = await db.CommunicationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return ToDto(preference);
    }

    public async Task UpdateAsync(string userId, CommunicationPreferencesDto preferences, CancellationToken cancellationToken = default)
    {
        var preference = await db.CommunicationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = CommunicationPreference.CreateDefault(userId);
            db.CommunicationPreferences.Add(preference);
        }

        preference.Update(
            preferences.EmailEnabled,
            preferences.InAppEnabled,
            preferences.MarketingEnabled,
            preferences.SecurityEnabled,
            preferences.OrderEnabled,
            preferences.MembershipEnabled,
            preferences.SupportEnabled,
            preferences.PromotionEnabled,
            preferences.GeneralEnabled);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsChannelEnabledAsync(string userId, string channel, CommunicationCategory category, CancellationToken cancellationToken = default)
    {
        var preference = await db.CommunicationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return preference is null
            ? CommunicationPreference.CreateDefault(userId).IsChannelEnabledFor(channel, category)
            : preference.IsChannelEnabledFor(channel, category);
    }

    private static CommunicationPreferencesDto ToDto(CommunicationPreference? preference)
    {
        if (preference is null)
        {
            var defaults = CommunicationPreference.CreateDefault("_");
            return new CommunicationPreferencesDto(
                defaults.EmailEnabled, defaults.InAppEnabled, defaults.MarketingEnabled, defaults.SecurityEnabled,
                defaults.OrderEnabled, defaults.MembershipEnabled, defaults.SupportEnabled, defaults.PromotionEnabled, defaults.GeneralEnabled);
        }

        return new CommunicationPreferencesDto(
            preference.EmailEnabled, preference.InAppEnabled, preference.MarketingEnabled, preference.SecurityEnabled,
            preference.OrderEnabled, preference.MembershipEnabled, preference.SupportEnabled, preference.PromotionEnabled, preference.GeneralEnabled);
    }
}
