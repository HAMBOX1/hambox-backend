namespace HAMBOX.Modules.Messaging.Application.RateLimiting;

/// <summary>Named ASP.NET Core rate limiting policy for the WhatsApp webhook — the one truly
/// anonymous, unauthenticated inbound surface this module exposes.</summary>
public static class MessagingRateLimitPolicies
{
    public const string WhatsAppWebhook = "whatsapp:webhook";
}
