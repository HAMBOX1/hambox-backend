using System.Text.Json;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Domain.Communication;

namespace HAMBOX.Modules.Communication.Infrastructure.Services;

/// <summary>
/// InApp channel provider. Writes into Commerce's existing <see cref="UserNotification"/> table — the
/// same table the live notification bell and /account/notifications page already read/write — rather
/// than introducing a second in-app notification store. This is the one place in the codebase that
/// creates <see cref="UserNotification"/> rows; business modules call <c>ICommunicationService</c> instead
/// of inserting one directly.
/// </summary>
internal sealed class DatabaseNotificationProvider(ICommerceDbContext commerceDb) : ICommunicationProvider
{
    public const string Key = CommunicationChannels.InApp;

    public string ChannelKey => Key;

    public async Task<CommunicationDeliveryResult> SendAsync(CommunicationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        var notification = UserNotification.Create(
            context.UserId,
            context.Subject,
            context.Body,
            context.Category,
            ExtractActionUrl(context.MetadataJson));

        commerceDb.UserNotifications.Add(notification);
        await commerceDb.SaveChangesAsync(cancellationToken);

        return CommunicationDeliveryResult.Ok();
    }

    private static string? ExtractActionUrl(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            return document.RootElement.TryGetProperty("actionUrl", out var value) ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
