using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.Services;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Messaging.Infrastructure.Providers;

/// <summary>
/// Local-development / test stand-in for the real WhatsApp provider. Does not call Meta — it just logs
/// the outbound message and remembers it so a test (or a developer replaying the webhook endpoint by
/// hand) can see exactly what the bot would have sent. Registered instead of a real provider whenever
/// Meta credentials are not configured (see <c>MessagingInfrastructureExtensions</c>) — never registered
/// alongside a real provider, and never holds or accepts anything resembling a production credential.
/// </summary>
public sealed class FakeWhatsAppProvider(ILogger<FakeWhatsAppProvider> logger) : IWhatsAppProvider
{
    public string ProviderName => "Fake";

    /// <summary>Every message sent through this provider, in call order — (phone number, message).
    /// Read directly by tests; a developer can also just watch the log output.</summary>
    public List<(string PhoneNumber, string Message)> SentMessages { get; } = [];

    public Task SendTextMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        SentMessages.Add((toPhoneNumber, message));

        // Reply text can carry order numbers, ticket subjects, product names, etc. — kept in-memory
        // here (for tests/local inspection) but deliberately never written to the persistent log at
        // Information level. Only length is logged at that level; full content is Debug-only, opt-in
        // for a developer actively troubleshooting locally. A real provider's delivery logging should
        // follow the same shape. Phone numbers are PII and are masked at every log level — never
        // written in full, even at Debug.
        var maskedPhoneNumber = WhatsAppPhoneMasker.Mask(toPhoneNumber);
        logger.LogInformation(
            "[FakeWhatsAppProvider] -> {PhoneNumber}: {MessageLength} chars", maskedPhoneNumber, message.Length);
        logger.LogDebug("[FakeWhatsAppProvider] -> {PhoneNumber}: {Message}", maskedPhoneNumber, message);

        return Task.CompletedTask;
    }
}
