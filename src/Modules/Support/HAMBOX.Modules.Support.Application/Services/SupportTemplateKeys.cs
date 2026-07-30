namespace HAMBOX.Modules.Support.Application.Services;

/// <summary>
/// Communication template keys used by the Support module. Rows for each of these must exist
/// in <c>communication.CommunicationTemplates</c> (seeded by <c>SupportCommunicationTemplateSeeder</c>
/// in Support.Infrastructure) or <see cref="HAMBOX.Application.Communication.ICommunicationService"/>
/// silently no-ops for that key.
/// </summary>
public static class SupportTemplateKeys
{
    public const string TicketCreated = "Support.TicketCreated";
    public const string TicketAssigned = "Support.TicketAssigned";
    public const string TicketStatusChanged = "Support.TicketStatusChanged";
    public const string TicketResolved = "Support.TicketResolved";
    public const string TicketClosed = "Support.TicketClosed";
    public const string NewReply = "Support.NewReply";
    public const string Mention = "Support.Mention";
}
