using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.Modules.Communication.Infrastructure.Persistence;
using HAMBOX.Modules.Messaging.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Messaging.Infrastructure.Persistence;

/// <summary>
/// Seeds the one Communication template <see cref="WhatsAppLinkVerificationService"/> needs
/// (<see cref="MessagingTemplateKeys.WhatsAppLinkVerificationCode"/>, Email channel only — there is no
/// InApp notification for this, the recipient hasn't linked yet) so <c>ICommunicationService.SendAsync</c>
/// actually delivers something instead of silently no-op'ing on an unseeded key. Mirrors
/// <c>SupportCommunicationTemplateSeeder</c>'s shape and direct reference to Communication.Infrastructure.
/// Idempotent — a no-op once the row exists.
/// </summary>
public static class MessagingCommunicationTemplateSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();

        if (await db.CommunicationTemplates.AnyAsync(t => t.Key == MessagingTemplateKeys.WhatsAppLinkVerificationCode))
        {
            return;
        }

        var template = CommunicationTemplate.Create(
            MessagingTemplateKeys.WhatsAppLinkVerificationCode, CommunicationChannels.Email, CommunicationCategory.Security);

        var version = template.CreateDraftVersion(
            "Your HAMBOX WhatsApp verification code",
            null,
            "<p>Your verification code is <strong>{{Code}}</strong>. It expires in {{ExpiresInMinutes}} minutes. "
                + "If you didn't request this, you can ignore this email.</p>",
            null,
            null);
        template.PublishVersion(version.Id, "system");

        db.CommunicationTemplates.Add(template);
        await db.SaveChangesAsync();
    }
}
