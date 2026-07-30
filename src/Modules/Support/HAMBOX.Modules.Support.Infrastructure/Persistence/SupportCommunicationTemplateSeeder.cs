using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.Modules.Communication.Infrastructure.Persistence;
using HAMBOX.Modules.Support.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Support.Infrastructure.Persistence;

/// <summary>
/// Seeds the Communication templates the Support module's <c>ICommunicationService.SendAsync</c>
/// calls need to actually deliver anything (every <see cref="SupportTemplateKeys"/> entry, InApp +
/// Email). Lives in Support.Infrastructure (with a direct reference to Communication.Infrastructure,
/// the same asymmetric-coupling shape Communication itself already uses against Commerce) rather
/// than in Communication, since Support owns the copy it needs seeded. Idempotent per key.
/// </summary>
public static class SupportCommunicationTemplateSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();

        await SeedIfMissing(db, SupportTemplateKeys.TicketCreated,
            "Ticket created", "Your support ticket {{TicketNumber}} (\"{{Subject}}\") has been created. We'll get back to you shortly.",
            "Your HAMBOX support ticket {{TicketNumber}} was created",
            "<p>Your support ticket <strong>{{TicketNumber}}</strong> (\"{{Subject}}\") has been created. We'll get back to you shortly.</p>");

        await SeedIfMissing(db, SupportTemplateKeys.TicketAssigned,
            "Ticket assigned to you", "Ticket {{TicketNumber}} (\"{{Subject}}\") has been assigned to you.",
            "Support ticket {{TicketNumber}} assigned to you",
            "<p>Support ticket <strong>{{TicketNumber}}</strong> (\"{{Subject}}\") has been assigned to you.</p>");

        await SeedIfMissing(db, SupportTemplateKeys.TicketStatusChanged,
            "Ticket status updated", "Ticket {{TicketNumber}} status changed to {{Status}}.",
            "Your HAMBOX support ticket {{TicketNumber}} status changed",
            "<p>Your support ticket <strong>{{TicketNumber}}</strong> status changed to {{Status}}.</p>");

        await SeedIfMissing(db, SupportTemplateKeys.TicketResolved,
            "Ticket resolved", "Ticket {{TicketNumber}} has been marked resolved. Let us know if you need anything else.",
            "Your HAMBOX support ticket {{TicketNumber}} was resolved",
            "<p>Your support ticket <strong>{{TicketNumber}}</strong> has been marked resolved. Let us know if you need anything else.</p>");

        await SeedIfMissing(db, SupportTemplateKeys.TicketClosed,
            "Ticket closed", "Ticket {{TicketNumber}} has been closed.",
            "Your HAMBOX support ticket {{TicketNumber}} was closed",
            "<p>Your support ticket <strong>{{TicketNumber}}</strong> has been closed.</p>");

        await SeedIfMissing(db, SupportTemplateKeys.NewReply,
            "New reply", "You have a new reply on ticket {{TicketNumber}}.",
            "New reply on your HAMBOX support ticket {{TicketNumber}}",
            "<p>You have a new reply on ticket <strong>{{TicketNumber}}</strong>.</p>");

        await SeedIfMissing(db, SupportTemplateKeys.Mention,
            "You were mentioned", "You were mentioned on ticket {{TicketNumber}}.",
            "You were mentioned on a HAMBOX support ticket",
            "<p>You were mentioned on ticket <strong>{{TicketNumber}}</strong>.</p>");

        await db.SaveChangesAsync();
    }

    private static async Task SeedIfMissing(
        CommunicationDbContext db, string key, string inAppSubject, string inAppBody, string emailSubject, string emailBody)
    {
        if (await db.CommunicationTemplates.AnyAsync(t => t.Key == key))
        {
            return;
        }

        SeedTemplate(db, key, CommunicationChannels.InApp, inAppSubject, inAppBody);
        SeedTemplate(db, key, CommunicationChannels.Email, emailSubject, emailBody);
    }

    private static void SeedTemplate(CommunicationDbContext db, string key, string channel, string subject, string body)
    {
        var template = CommunicationTemplate.Create(key, channel, CommunicationCategory.Support);
        var version = template.CreateDraftVersion(subject, null, body, null, null);
        template.PublishVersion(version.Id, "system");
        db.CommunicationTemplates.Add(template);
    }
}
