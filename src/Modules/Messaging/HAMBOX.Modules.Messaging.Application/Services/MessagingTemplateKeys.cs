namespace HAMBOX.Modules.Messaging.Application.Services;

/// <summary>
/// Communication template keys used by the Messaging module. Rows for each of these must exist in
/// <c>communication.CommunicationTemplates</c> (seeded by <c>MessagingCommunicationTemplateSeeder</c>
/// in Messaging.Infrastructure) or <c>ICommunicationService</c> silently no-ops for that key.
/// </summary>
public static class MessagingTemplateKeys
{
    public const string WhatsAppLinkVerificationCode = "Messaging.WhatsAppLinkVerificationCode";
}
