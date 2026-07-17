namespace HAMBOX.Modules.Communication.Application.Abstractions;

/// <summary>
/// Everything a channel provider needs to deliver one rendered message. Providers never receive the
/// storage entity itself — this is the seam that keeps provider implementations persistence-agnostic,
/// mirroring <c>IBackgroundJobContext</c>/<c>SupplierProviderContext</c>.
/// </summary>
public sealed record CommunicationDeliveryContext(
    Guid MessageId,
    string UserId,
    string Category,
    string Subject,
    string Body,
    string? CorrelationId,
    string? MetadataJson,
    string? RelatedEntityType,
    string? RelatedEntityId);

public sealed record CommunicationDeliveryResult(bool Success, string? ErrorMessage = null)
{
    public static CommunicationDeliveryResult Ok() => new(true);

    public static CommunicationDeliveryResult Fail(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// A single delivery channel (InApp, Email, and — as future extension points only, not implemented here —
/// Sms/WhatsApp/Telegram/Discord/Slack/Push/Webhook). The platform core only ever depends on this
/// interface and <see cref="ICommunicationProviderRegistry"/> — adding a channel later is exactly:
/// implement this interface, register it in DI. Mirrors <c>ISupplierProvider</c>.
/// </summary>
public interface ICommunicationProvider
{
    /// <summary>Key resolved by <see cref="ICommunicationProviderRegistry"/> (e.g. <c>CommunicationChannels.Email</c>).</summary>
    string ChannelKey { get; }

    Task<CommunicationDeliveryResult> SendAsync(CommunicationDeliveryContext context, CancellationToken cancellationToken = default);
}
