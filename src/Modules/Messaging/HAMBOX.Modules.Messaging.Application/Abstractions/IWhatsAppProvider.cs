namespace HAMBOX.Modules.Messaging.Application.Abstractions;

/// <summary>
/// The one thing the bot engine depends on to actually deliver a WhatsApp message. Everything
/// Meta-specific (access tokens, phone number IDs, the Graph API call, signature verification on the
/// way in) lives behind an implementation of this interface — the engine never talks to Meta directly.
/// <see cref="Infrastructure.Providers.FakeWhatsAppProvider"/> (dev/test) and a future
/// <c>MetaWhatsAppProvider</c> (once credentials exist) are the only two implementations this needs.
/// </summary>
public interface IWhatsAppProvider
{
    /// <summary>Short identifier for logs/diagnostics, e.g. "Fake" or "Meta".</summary>
    string ProviderName { get; }

    /// <summary>
    /// Sends one plain-text message (a menu screen, a confirmation, an error) to a phone number in
    /// E.164 format. Menus are rendered as plain numbered text, not native interactive
    /// lists/buttons — deliberately, since the real provider isn't wired up yet and a numbered-text
    /// menu works identically over both a fake and a real WhatsApp session.
    /// </summary>
    Task SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);
}
