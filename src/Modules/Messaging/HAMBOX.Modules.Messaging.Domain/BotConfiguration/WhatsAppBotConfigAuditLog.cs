using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Messaging.Domain.BotConfiguration;

/// <summary>
/// One row per meaningful configuration change (never per raw HTTP call) — e.g. "Orders enabled",
/// "Order changed", "Label changed for Cart (en)". Never records customer WhatsApp message content;
/// this only tracks admin edits to presentation configuration.
/// </summary>
public sealed class WhatsAppBotConfigAuditLog : Entity
{
    private WhatsAppBotConfigAuditLog()
    {
    }

    private WhatsAppBotConfigAuditLog(
        Guid id, WhatsAppBotConfigAuditAction action, string? target, string? oldValue, string? newValue, string? actorUserId)
        : base(id)
    {
        Action = action;
        Target = target;
        OldValue = oldValue;
        NewValue = newValue;
        ActorUserId = actorUserId;
    }

    public WhatsAppBotConfigAuditAction Action { get; private set; }

    /// <summary>What changed — a <see cref="WhatsAppMenuAction"/> name for item-level changes, or a
    /// fixed label like "WelcomeMessage"/"FallbackMessage" for the singleton fields.</summary>
    public string? Target { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public string? ActorUserId { get; private set; }

    private const int ValueMaxLength = 1000; // matches WhatsAppBotConfigAuditLogConfiguration's HasMaxLength(1000)

    public static WhatsAppBotConfigAuditLog Create(
        WhatsAppBotConfigAuditAction action, string? target, string? oldValue, string? newValue, string? actorUserId) =>
        new(Guid.NewGuid(), action, target, Truncate(oldValue), Truncate(newValue), actorUserId);

    // Welcome/fallback messages are each validated up to 500 chars EN + 500 chars AR, composed here as
    // "en / ar" (1003 chars) before ever reaching this audit row — one char over the column's 1000-char
    // cap is enough to throw a truncation DbUpdateException on an otherwise perfectly valid save.
    // Truncating here (the single place every RecordAudit call funnels through) is the fix, not raising
    // the column width — this is an audit trail, not the source of truth for the actual message text.
    private static string? Truncate(string? value) =>
        value is not null && value.Length > ValueMaxLength ? value[..ValueMaxLength] : value;
}
