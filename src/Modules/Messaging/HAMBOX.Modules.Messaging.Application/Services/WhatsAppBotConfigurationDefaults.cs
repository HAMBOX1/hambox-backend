using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Domain.BotConfiguration;

namespace HAMBOX.Modules.Messaging.Application.Services;

/// <summary>
/// The original hardcoded V1 menu text, now the single source of both (a) the DB seed data an admin
/// starts from and (b) the in-memory fallback <see cref="IWhatsAppBotConfigurationProvider"/> returns if
/// the database is ever unseeded or every item somehow ends up disabled — the bot must never go mute or
/// throw just because configuration is missing or malformed.
/// </summary>
public static class WhatsAppBotConfigurationDefaults
{
    public const string WelcomeMessageEn = "HAMBOX Main Menu:";
    public const string WelcomeMessageAr = "قائمة HAMBOX الرئيسية:";
    public const string FallbackMessageEn = "Sorry, I didn't understand that.";
    public const string FallbackMessageAr = "عذرًا، لم أفهم ذلك.";

    public static readonly IReadOnlyList<(WhatsAppMenuAction Action, string LabelEn, string LabelAr)> Items =
    [
        (WhatsAppMenuAction.BrowseProducts, "Browse Games", "تصفح الألعاب"),
        (WhatsAppMenuAction.SearchProducts, "Search Games", "بحث عن لعبة"),
        (WhatsAppMenuAction.Cart, "My Cart", "سلة التسوق"),
        (WhatsAppMenuAction.Orders, "My Orders", "طلباتي"),
        (WhatsAppMenuAction.Alerts, "My Alerts", "تنبيهاتي"),
        (WhatsAppMenuAction.Support, "Support", "الدعم"),
        (WhatsAppMenuAction.Language, "Language", "اللغة"),
    ];

    public static readonly WhatsAppBotMenuSnapshot Snapshot = new(
        WelcomeMessageEn, WelcomeMessageAr, FallbackMessageEn, FallbackMessageAr,
        Items.Select(i => new WhatsAppMenuItemSnapshot(i.Action, i.LabelEn, i.LabelAr)).ToList());
}
