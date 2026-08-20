using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Messaging.Domain.BotConfiguration;

/// <summary>
/// One row per <see cref="WhatsAppMenuAction"/> — exactly seven, seeded once, never created or deleted
/// after that (see <see cref="Action"/>: a fixed enum, not a free-text field). Admins may only toggle
/// <see cref="IsEnabled"/>, change <see cref="SortOrder"/>, and edit the two labels. None of that
/// changes what the action does or who is allowed to reach it — <c>WhatsAppBotEngine</c> still owns
/// every state transition and authorization check; this only controls whether/where/how the action is
/// offered on the numbered main menu.
/// </summary>
public sealed class WhatsAppMenuItem : Entity, IAuditable
{
    private WhatsAppMenuItem()
    {
    }

    private WhatsAppMenuItem(Guid id, WhatsAppMenuAction action, int sortOrder, string labelEn, string labelAr)
        : base(id)
    {
        Action = action;
        SortOrder = sortOrder;
        LabelEn = labelEn;
        LabelAr = labelAr;
        IsEnabled = true;
    }

    public WhatsAppMenuAction Action { get; private set; }

    public bool IsEnabled { get; private set; }

    public int SortOrder { get; private set; }

    public string LabelEn { get; private set; } = string.Empty;

    public string LabelAr { get; private set; } = string.Empty;

    public string? CreatedBy { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static WhatsAppMenuItem CreateDefault(WhatsAppMenuAction action, int sortOrder, string labelEn, string labelAr) =>
        new(Guid.NewGuid(), action, sortOrder, labelEn, labelAr);

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    public void SetLabels(string labelEn, string labelAr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labelEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelAr);
        LabelEn = labelEn;
        LabelAr = labelAr;
    }
}
