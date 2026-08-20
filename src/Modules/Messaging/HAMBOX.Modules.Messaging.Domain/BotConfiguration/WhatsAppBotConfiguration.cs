using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Messaging.Domain.BotConfiguration;

/// <summary>
/// Singleton row (exactly one, seeded once) holding the bot's welcome and fallback text. Presentation
/// only — nothing here affects state transitions, authorization, or which underlying handler a menu
/// action reaches; see <see cref="WhatsAppMenuItem"/> for the per-action rows.
/// </summary>
public sealed class WhatsAppBotConfiguration : AggregateRoot, IAuditable
{
    private WhatsAppBotConfiguration()
    {
    }

    private WhatsAppBotConfiguration(
        Guid id, string welcomeMessageEn, string welcomeMessageAr, string fallbackMessageEn, string fallbackMessageAr)
        : base(id)
    {
        WelcomeMessageEn = welcomeMessageEn;
        WelcomeMessageAr = welcomeMessageAr;
        FallbackMessageEn = fallbackMessageEn;
        FallbackMessageAr = fallbackMessageAr;
    }

    public string WelcomeMessageEn { get; private set; } = string.Empty;

    public string WelcomeMessageAr { get; private set; } = string.Empty;

    public string FallbackMessageEn { get; private set; } = string.Empty;

    public string FallbackMessageAr { get; private set; } = string.Empty;

    public string? CreatedBy { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static WhatsAppBotConfiguration CreateDefault(
        string welcomeMessageEn, string welcomeMessageAr, string fallbackMessageEn, string fallbackMessageAr) =>
        new(Guid.NewGuid(), welcomeMessageEn, welcomeMessageAr, fallbackMessageEn, fallbackMessageAr);

    public void SetWelcomeMessage(string en, string ar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(en);
        ArgumentException.ThrowIfNullOrWhiteSpace(ar);
        WelcomeMessageEn = en;
        WelcomeMessageAr = ar;
    }

    public void SetFallbackMessage(string en, string ar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(en);
        ArgumentException.ThrowIfNullOrWhiteSpace(ar);
        FallbackMessageEn = en;
        FallbackMessageAr = ar;
    }
}
