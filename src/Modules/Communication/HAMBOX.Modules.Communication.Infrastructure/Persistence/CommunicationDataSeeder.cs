using HAMBOX.Modules.Communication.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Communication.Infrastructure.Persistence;

/// <summary>
/// Seeds the two default provider configs and the templates the built-in Commerce integration
/// (order confirmation, membership confirmation, resent codes) needs to actually deliver anything.
/// Idempotent — a no-op once any row exists for a given key.
/// </summary>
public static class CommunicationDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();

        if (!await db.CommunicationProviderConfigs.AnyAsync())
        {
            db.CommunicationProviderConfigs.Add(CommunicationProviderConfig.Create(
                "Database Notifications", DatabaseNotificationProviderKey, CommunicationChannels.InApp, priority: 0));
            db.CommunicationProviderConfigs.Add(CommunicationProviderConfig.Create(
                "SMTP Email", EmailProviderKey, CommunicationChannels.Email, priority: 1));
        }

        if (!await db.CommunicationTemplates.AnyAsync())
        {
            SeedTemplate(db, "OrderConfirmation", CommunicationChannels.InApp, CommunicationCategory.Order,
                "Order Completed", "Your order {{OrderNumber}} is complete. License keys are ready in My Library.");
            SeedTemplate(db, "OrderConfirmation", CommunicationChannels.Email, CommunicationCategory.Order,
                "Your HAMBOX order {{OrderNumber}} is complete",
                "<p>Your order <strong>{{OrderNumber}}</strong> (total {{Total}}) is complete. Your license keys are ready in your library.</p>");

            SeedTemplate(db, "MembershipConfirmation", CommunicationChannels.InApp, CommunicationCategory.Membership,
                "Membership {{ActionLabel}}", "Your {{PlanName}} membership has been {{ActionLabel}}. Order {{OrderNumber}} is complete.");
            SeedTemplate(db, "MembershipConfirmation", CommunicationChannels.Email, CommunicationCategory.Membership,
                "Your {{PlanName}} membership has been {{ActionLabel}}",
                "<p>Your <strong>{{PlanName}}</strong> membership has been {{ActionLabel}}. Order {{OrderNumber}} is complete.</p>");

            SeedTemplate(db, "CodesResent", CommunicationChannels.InApp, CommunicationCategory.Order,
                "Digital codes resent", "{{CodeCount}} digital product code(s) for order {{OrderNumber}} were resent. Open your library to view them.");
            SeedTemplate(db, "CodesResent", CommunicationChannels.Email, CommunicationCategory.Order,
                "Your HAMBOX digital codes were resent",
                "<p>{{CodeCount}} digital product code(s) for order <strong>{{OrderNumber}}</strong> were resent. Open your library to view them.</p>");
        }

        // Seeded separately (not gated by the "any template exists" check above) since it was
        // added after the initial seed and must still land in databases that already have rows.
        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "SecurityAlert"))
        {
            SeedTemplate(db, "SecurityAlert", CommunicationChannels.InApp, CommunicationCategory.Security,
                "Security alert: {{EventType}}", "{{Description}}");
            SeedTemplate(db, "SecurityAlert", CommunicationChannels.Email, CommunicationCategory.Security,
                "HAMBOX security alert: {{EventType}}",
                "<p><strong>{{Severity}}</strong> security event at {{OccurredOnUtc}} UTC:</p><p>{{Description}}</p><p>IP address: {{IpAddress}}</p>");
        }

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "ReferralFriendJoined"))
        {
            SeedTemplate(db, "ReferralFriendJoined", CommunicationChannels.InApp, CommunicationCategory.Promotion,
                "A friend joined using your referral code",
                "Someone signed up using your referral code {{ReferralCode}}. You'll earn points once they complete their first order.");
            SeedTemplate(db, "ReferralFriendJoined", CommunicationChannels.Email, CommunicationCategory.Promotion,
                "Someone joined HAMBOX using your referral code",
                "<p>A new member signed up using your referral code <strong>{{ReferralCode}}</strong>. You'll earn referral points once they complete their first order.</p>");
        }

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "ReferralRewardEarned"))
        {
            SeedTemplate(db, "ReferralRewardEarned", CommunicationChannels.InApp, CommunicationCategory.Promotion,
                "You earned {{Points}} referral points",
                "Your referral completed their first order. {{Points}} points were added to your referral balance.");
            SeedTemplate(db, "ReferralRewardEarned", CommunicationChannels.Email, CommunicationCategory.Promotion,
                "You earned {{Points}} HAMBOX referral points",
                "<p>Your referral completed their first order and <strong>{{Points}} points</strong> were added to your referral balance.</p>");
        }

        await db.SaveChangesAsync();
    }

    public const string DatabaseNotificationProviderKey = "Database";
    public const string EmailProviderKey = "Smtp";

    private static void SeedTemplate(
        CommunicationDbContext db, string key, string channel, CommunicationCategory category, string subjectEn, string bodyEn)
    {
        var template = CommunicationTemplate.Create(key, channel, category);
        var version = template.CreateDraftVersion(subjectEn, null, bodyEn, null, null);
        template.PublishVersion(version.Id, "system");
        db.CommunicationTemplates.Add(template);
    }
}
