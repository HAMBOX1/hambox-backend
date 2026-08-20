using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HAMBOX.Modules.Messaging.Infrastructure.Services;

/// <summary>
/// Mirrors <c>PlatformSettingsService</c>'s <c>IMemoryCache</c> cache-aside pattern: one fixed cache key,
/// a 10-minute safety-net TTL, explicit <see cref="Invalidate"/> on write so a save takes effect on the
/// very next inbound message rather than waiting out the TTL. Never throws and never returns an empty
/// menu — falls back to <see cref="WhatsAppBotConfigurationDefaults"/> if the database is unseeded or
/// every item is somehow disabled, so the bot can never go mute because of missing/bad configuration.
/// </summary>
public sealed class WhatsAppBotConfigurationProvider(IMessagingDbContext db, IMemoryCache cache)
    : IWhatsAppBotConfigurationProvider
{
    private const string CacheKey = "whatsapp-bot:menu-config";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public Task<WhatsAppBotMenuSnapshot> GetAsync(CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await LoadAsync(cancellationToken);
        })!;

    public void Invalidate() => cache.Remove(CacheKey);

    private async Task<WhatsAppBotMenuSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var config = await db.WhatsAppBotConfigurations.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var enabledItems = await db.WhatsAppMenuItems.AsNoTracking()
            .Where(i => i.IsEnabled)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);

        if (config is null || enabledItems.Count == 0)
        {
            return WhatsAppBotConfigurationDefaults.Snapshot;
        }

        return new WhatsAppBotMenuSnapshot(
            config.WelcomeMessageEn,
            config.WelcomeMessageAr,
            config.FallbackMessageEn,
            config.FallbackMessageAr,
            enabledItems
                .Select(i => new WhatsAppMenuItemSnapshot(
                    i.Action,
                    string.IsNullOrWhiteSpace(i.LabelEn) ? i.Action.ToString() : i.LabelEn,
                    string.IsNullOrWhiteSpace(i.LabelAr) ? i.Action.ToString() : i.LabelAr))
                .ToList());
    }
}
