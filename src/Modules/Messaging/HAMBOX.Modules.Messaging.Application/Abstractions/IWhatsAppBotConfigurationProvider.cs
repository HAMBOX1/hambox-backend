using HAMBOX.Modules.Messaging.Domain.BotConfiguration;

namespace HAMBOX.Modules.Messaging.Application.Abstractions;

/// <summary>One enabled main-menu item, in display order, with the label for each language already
/// resolved to a safe non-empty value.</summary>
public sealed record WhatsAppMenuItemSnapshot(WhatsAppMenuAction Action, string LabelEn, string LabelAr);

/// <summary>Everything the bot engine needs to render the Main menu for one inbound message — loaded
/// once per message (see <see cref="IWhatsAppBotConfigurationProvider"/>), never re-queried per menu
/// item. Presentation only: it carries no authorization information and changing it can never grant or
/// revoke access to a state — <c>WhatsAppBotEngine</c> still owns every transition and security check.</summary>
public sealed record WhatsAppBotMenuSnapshot(
    string WelcomeMessageEn,
    string WelcomeMessageAr,
    string FallbackMessageEn,
    string FallbackMessageAr,
    IReadOnlyList<WhatsAppMenuItemSnapshot> EnabledItemsInOrder);

/// <summary>
/// Read path for the bot's admin-configurable presentation (welcome/fallback text, menu item
/// labels/order/enabled state). Implemented in Infrastructure with a small cache (mirrors
/// <c>PlatformSettingsService</c>'s <c>IMemoryCache</c> pattern) so the engine can call
/// <see cref="GetAsync"/> once per inbound message without hitting the database every time.
/// </summary>
public interface IWhatsAppBotConfigurationProvider
{
    Task<WhatsAppBotMenuSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the cached snapshot — called by the update command handler right after saving,
    /// so the very next inbound message (no restart required) sees the new configuration.</summary>
    void Invalidate();
}
