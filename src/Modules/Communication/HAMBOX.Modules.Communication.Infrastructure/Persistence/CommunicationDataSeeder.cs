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

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "BackInStockAlert"))
        {
            SeedTemplate(db, "BackInStockAlert", CommunicationChannels.InApp, CommunicationCategory.BackInStock,
                "{{ProductName}} is back in stock",
                "{{VariantLabel}} is available again. Get it before it's gone.",
                subjectAr: "{{ProductName}} متوفر الآن",
                bodyAr: "{{VariantLabel}} متوفر مجددًا. احصل عليه قبل نفاد الكمية.");
            SeedTemplate(db, "BackInStockAlert", CommunicationChannels.Email, CommunicationCategory.BackInStock,
                "{{ProductName}} is back in stock",
                "<p><strong>{{ProductName}}</strong> ({{VariantLabel}}) is available again. Get it before it's gone.</p>",
                subjectAr: "{{ProductName}} متوفر الآن",
                bodyAr: "<p><strong>{{ProductName}}</strong> ({{VariantLabel}}) متوفر مجددًا. احصل عليه قبل نفاد الكمية.</p>");
        }

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "PriceDropAlert"))
        {
            SeedTemplate(db, "PriceDropAlert", CommunicationChannels.InApp, CommunicationCategory.PriceDrop,
                "Price drop: {{ProductName}}",
                "{{VariantLabel}} dropped from {{OldPrice}} to {{NewPrice}}.",
                subjectAr: "انخفاض السعر: {{ProductName}}",
                bodyAr: "انخفض سعر {{VariantLabel}} من {{OldPrice}} إلى {{NewPrice}}.");
            SeedTemplate(db, "PriceDropAlert", CommunicationChannels.Email, CommunicationCategory.PriceDrop,
                "Price drop: {{ProductName}}",
                "<p><strong>{{ProductName}}</strong> ({{VariantLabel}}) dropped from {{OldPrice}} to <strong>{{NewPrice}}</strong>.</p>",
                subjectAr: "انخفاض السعر: {{ProductName}}",
                bodyAr: "<p>انخفض سعر <strong>{{ProductName}}</strong> ({{VariantLabel}}) من {{OldPrice}} إلى <strong>{{NewPrice}}</strong>.</p>");
        }

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "EliteExpiringSoon"))
        {
            SeedTemplate(db, "EliteExpiringSoon", CommunicationChannels.InApp, CommunicationCategory.Membership,
                "Your {{PlanName}} membership ends soon",
                "Your {{PlanName}} membership ends on {{EndDate}}. It will not renew automatically and you will not be charged — renew early to keep your remaining time.",
                subjectAr: "اشتراك {{PlanName}} ينتهي قريبًا",
                bodyAr: "ينتهي اشتراك {{PlanName}} في {{EndDate}}. لن يتجدد تلقائيًا ولن يُخصم منك أي مبلغ — جدده مبكرًا للاحتفاظ بالمدة المتبقية.");
            SeedTemplate(db, "EliteExpiringSoon", CommunicationChannels.Email, CommunicationCategory.Membership,
                "Your {{PlanName}} membership ends soon",
                "<p>Your <strong>{{PlanName}}</strong> membership ends on <strong>{{EndDate}}</strong>. It will not renew automatically and you will not be charged. Renew early to keep your remaining time.</p>",
                subjectAr: "اشتراك {{PlanName}} ينتهي قريبًا",
                bodyAr: "<p>ينتهي اشتراك <strong>{{PlanName}}</strong> في <strong>{{EndDate}}</strong>. لن يتجدد تلقائيًا ولن يُخصم منك أي مبلغ. جدده مبكرًا للاحتفاظ بالمدة المتبقية.</p>");
        }

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "EliteExpired"))
        {
            SeedTemplate(db, "EliteExpired", CommunicationChannels.InApp, CommunicationCategory.Membership,
                "Your {{PlanName}} membership has ended",
                "Your {{PlanName}} membership ended on {{EndDate}}. Your rank design and Stars are untouched.",
                subjectAr: "انتهى اشتراك {{PlanName}}",
                bodyAr: "انتهى اشتراك {{PlanName}} في {{EndDate}}. تصميم رتبتك ونجومك كما هي.");
            SeedTemplate(db, "EliteExpired", CommunicationChannels.Email, CommunicationCategory.Membership,
                "Your {{PlanName}} membership has ended",
                "<p>Your <strong>{{PlanName}}</strong> membership ended on {{EndDate}}. Your rank design and your Stars are untouched. You can subscribe again at any time.</p>",
                subjectAr: "انتهى اشتراك {{PlanName}}",
                bodyAr: "<p>انتهى اشتراك <strong>{{PlanName}}</strong> في {{EndDate}}. تصميم رتبتك ونجومك كما هي. يمكنك الاشتراك مجددًا في أي وقت.</p>");
        }

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "StarsExpiringSoon"))
        {
            SeedTemplate(db, "StarsExpiringSoon", CommunicationChannels.InApp, CommunicationCategory.System,
                "{{Stars}} Stars expire soon", "{{Stars}} of your Stars expire on {{Date}}. Spend them in the Stars Shop before they lapse.",
                subjectAr: "{{Stars}} نجمة ستنتهي قريبًا", bodyAr: "ستنتهي صلاحية {{Stars}} من نجومك في {{Date}}. اصرفها في متجر النجوم قبل أن تنتهي.");
            SeedTemplate(db, "StarsExpiringSoon", CommunicationChannels.Email, CommunicationCategory.System,
                "{{Stars}} Stars expire soon", "<p><strong>{{Stars}}</strong> of your Stars expire on <strong>{{Date}}</strong>. Spend them in the Stars Shop before they lapse.</p>",
                subjectAr: "{{Stars}} نجمة ستنتهي قريبًا", bodyAr: "<p>ستنتهي صلاحية <strong>{{Stars}}</strong> من نجومك في <strong>{{Date}}</strong>. اصرفها في متجر النجوم قبل أن تنتهي.</p>");
        }

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "StarsCampaignStarted"))
        {
            SeedTemplate(db, "StarsCampaignStarted", CommunicationChannels.InApp, CommunicationCategory.Marketing,
                "{{Offer}} Stars on a product you follow", "{{Campaign}} has started: earn {{Offer}} Stars on a product in your wishlist.",
                subjectAr: "{{Offer}} نجوم على منتج تتابعه", bodyAr: "بدأت حملة {{Campaign}}: اكسب {{Offer}} نجوم على منتج في قائمة مفضلتك.");
            SeedTemplate(db, "StarsCampaignStarted", CommunicationChannels.Email, CommunicationCategory.Marketing,
                "{{Offer}} Stars on a product you follow", "<p><strong>{{Campaign}}</strong> has started: earn <strong>{{Offer}}</strong> Stars on a product in your wishlist.</p>",
                subjectAr: "{{Offer}} نجوم على منتج تتابعه", bodyAr: "<p>بدأت حملة <strong>{{Campaign}}</strong>: اكسب <strong>{{Offer}}</strong> نجوم على منتج في قائمة مفضلتك.</p>");
        }

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "RankUp"))
        {
            SeedTemplate(db, "RankUp", CommunicationChannels.InApp, CommunicationCategory.Promotion,
                "You reached {{Rank}}", "Congratulations — you are now {{Rank}}. Your new rank design is ready.",
                subjectAr: "وصلت إلى رتبة {{RankAr}}", bodyAr: "تهانينا — أصبحت الآن {{RankAr}}. تصميم رتبتك الجديد جاهز.");
        }

        if (!await db.CommunicationTemplates.AnyAsync(t => t.Key == "MissionCompleted"))
        {
            SeedTemplate(db, "MissionCompleted", CommunicationChannels.InApp, CommunicationCategory.Promotion,
                "Mission completed: {{Mission}}", "You completed \"{{Mission}}\" and earned {{Stars}} Stars.",
                subjectAr: "أنجزت المهمة: {{MissionAr}}", bodyAr: "أنجزت «{{MissionAr}}» وكسبت {{Stars}} نجمة.");
        }

        await db.SaveChangesAsync();

        // Older databases hold English-only copies of the default templates: add the Arabic where it is still missing.
        await DefaultArabicTemplateCopy.BackfillAsync(db);
    }

    public const string DatabaseNotificationProviderKey = "Database";
    public const string EmailProviderKey = "Smtp";

    private static void SeedTemplate(
        CommunicationDbContext db,
        string key,
        string channel,
        CommunicationCategory category,
        string subjectEn,
        string bodyEn,
        string? subjectAr = null,
        string? bodyAr = null)
    {
        var arabic = DefaultArabicTemplateCopy.For(key, channel);
        var template = CommunicationTemplate.Create(key, channel, category);
        var version = template.CreateDraftVersion(subjectEn, subjectAr ?? arabic.Subject, bodyEn, bodyAr ?? arabic.Body, null);
        template.PublishVersion(version.Id, "system");
        db.CommunicationTemplates.Add(template);
    }
}
