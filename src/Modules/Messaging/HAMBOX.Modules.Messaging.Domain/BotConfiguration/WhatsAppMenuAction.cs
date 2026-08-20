namespace HAMBOX.Modules.Messaging.Domain.BotConfiguration;

/// <summary>
/// The fixed, closed set of actions a WhatsApp main-menu item can trigger. Deliberately a plain enum,
/// not an open string — admins can enable/disable/reorder/relabel these seven, never invent a new one
/// or delete one of these. Adding a new action requires a code change (a new engine handler to dispatch
/// to), not an admin-editable field.
/// </summary>
public enum WhatsAppMenuAction
{
    BrowseProducts = 0,
    SearchProducts = 1,
    Cart = 2,
    Orders = 3,
    Alerts = 4,
    Support = 5,
    Language = 6,
}
