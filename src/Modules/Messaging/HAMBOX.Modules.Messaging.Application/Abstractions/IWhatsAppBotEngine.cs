namespace HAMBOX.Modules.Messaging.Application.Abstractions;

/// <summary>One inbound WhatsApp text message, already unwrapped from whatever the provider's
/// webhook payload shape is. <see cref="FromPhoneNumber"/> is E.164 (e.g. "+201234567890").</summary>
public sealed record WhatsAppInboundMessage(string FromPhoneNumber, string Text);

/// <summary>
/// The menu-driven conversation engine. Takes one inbound message, resolves/advances the caller's
/// <c>WhatsAppConversationSession</c>, dispatches to existing HAMBOX MediatR queries/commands for any
/// real data, and sends the reply back through <see cref="IWhatsAppProvider"/>. This is the only entry
/// point the webhook endpoint calls — it never talks to Catalog/Commerce/Support or the provider directly.
/// </summary>
public interface IWhatsAppBotEngine
{
    Task HandleInboundMessageAsync(WhatsAppInboundMessage message, CancellationToken cancellationToken = default);
}
