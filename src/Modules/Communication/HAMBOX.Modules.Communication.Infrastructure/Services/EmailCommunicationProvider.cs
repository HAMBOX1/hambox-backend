using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Infrastructure.Services;

/// <summary>
/// Email channel provider. Delegates the actual send to <see cref="IEmailService"/> — the same
/// MailKit/Platform-Settings-driven SMTP path the three existing identity email flows already use — so
/// there is exactly one place in the codebase that talks SMTP.
/// </summary>
internal sealed class EmailCommunicationProvider(IIdentityDbContext identityDb, IEmailService emailService) : ICommunicationProvider
{
    public const string Key = CommunicationChannels.Email;

    public string ChannelKey => Key;

    public async Task<CommunicationDeliveryResult> SendAsync(CommunicationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(context.UserId, out var userId))
        {
            return CommunicationDeliveryResult.Fail("Invalid user id.");
        }

        var email = await identityDb.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(email))
        {
            return CommunicationDeliveryResult.Fail("User has no email address on file.");
        }

        try
        {
            await emailService.SendTemplatedEmailAsync(email, context.Subject, context.Body, context.CorrelationId, cancellationToken);
            return CommunicationDeliveryResult.Ok();
        }
        catch (Exception ex)
        {
            return CommunicationDeliveryResult.Fail(ex.Message);
        }
    }
}
