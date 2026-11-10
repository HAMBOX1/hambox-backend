using HAMBOX.Modules.Communication.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Communication.Infrastructure.Persistence;

/// <summary>
/// Arabic copy for the system templates that were seeded English-only, so a customer whose preferred language is
/// Arabic stops receiving English notifications and e-mails. Existing databases already hold the English rows, so
/// <see cref="BackfillAsync"/> adds a new published version (same English text, plus the Arabic) wherever the
/// Arabic is still missing; a template an admin has already translated is left alone. Idempotent.
/// </summary>
public static class DefaultArabicTemplateCopy
{
    private static readonly Dictionary<(string Key, string Channel), (string Subject, string Body)> Copy = new()
    {
        [("OrderConfirmation", CommunicationChannels.InApp)] = ("اكتمل الطلب", "اكتمل طلبك {{OrderNumber}}. مفاتيح التفعيل جاهزة في مكتبتي."),
        [("OrderConfirmation", CommunicationChannels.Email)] = ("اكتمل طلبك {{OrderNumber}} في HAMBOX", "<p>اكتمل طلبك <strong>{{OrderNumber}}</strong> (الإجمالي {{Total}}). مفاتيح التفعيل جاهزة في مكتبتك.</p>"),
        [("MembershipConfirmation", CommunicationChannels.InApp)] = ("تم تحديث عضويتك", "تم تحديث عضوية {{PlanName}}. اكتمل الطلب {{OrderNumber}}."),
        [("MembershipConfirmation", CommunicationChannels.Email)] = ("تم تحديث عضوية {{PlanName}}", "<p>تم تحديث عضوية <strong>{{PlanName}}</strong>. اكتمل الطلب {{OrderNumber}}.</p>"),
        [("CodesResent", CommunicationChannels.InApp)] = ("أُعيد إرسال الأكواد الرقمية", "أُعيد إرسال {{CodeCount}} من الأكواد الرقمية للطلب {{OrderNumber}}. افتح مكتبتك لعرضها."),
        [("CodesResent", CommunicationChannels.Email)] = ("أُعيد إرسال أكوادك الرقمية من HAMBOX", "<p>أُعيد إرسال {{CodeCount}} من الأكواد الرقمية للطلب <strong>{{OrderNumber}}</strong>. افتح مكتبتك لعرضها.</p>"),
        [("SecurityAlert", CommunicationChannels.InApp)] = ("تنبيه أمني: {{EventType}}", "{{Description}}"),
        [("SecurityAlert", CommunicationChannels.Email)] = ("تنبيه أمني من HAMBOX: {{EventType}}", "<p>حدث أمني (<strong>{{Severity}}</strong>) في {{OccurredOnUtc}} بتوقيت UTC:</p><p>{{Description}}</p><p>عنوان IP: {{IpAddress}}</p>"),
        [("ReferralFriendJoined", CommunicationChannels.InApp)] = ("انضم صديق باستخدام رمز الإحالة الخاص بك", "سجّل شخص ما باستخدام رمز الإحالة {{ReferralCode}}. ستحصل على نقاط عندما يُكمل أول طلب له."),
        [("ReferralFriendJoined", CommunicationChannels.Email)] = ("انضم شخص إلى HAMBOX باستخدام رمز الإحالة الخاص بك", "<p>سجّل عضو جديد باستخدام رمز الإحالة <strong>{{ReferralCode}}</strong>. ستحصل على نقاط الإحالة عندما يُكمل أول طلب له.</p>"),
        [("ReferralRewardEarned", CommunicationChannels.InApp)] = ("ربحت {{Points}} نقطة إحالة", "أكمل صديقك أول طلب له. أُضيفت {{Points}} نقطة إلى رصيد الإحالة الخاص بك."),
        [("ReferralRewardEarned", CommunicationChannels.Email)] = ("ربحت {{Points}} نقطة إحالة في HAMBOX", "<p>أكمل صديقك أول طلب له وأُضيفت <strong>{{Points}} نقطة</strong> إلى رصيد الإحالة الخاص بك.</p>"),
        [("Support.TicketCreated", CommunicationChannels.InApp)] = ("تم إنشاء التذكرة", "تم إنشاء تذكرة الدعم {{TicketNumber}} (\"{{Subject}}\"). سنرد عليك قريبًا."),
        [("Support.TicketCreated", CommunicationChannels.Email)] = ("تم إنشاء تذكرة الدعم {{TicketNumber}} في HAMBOX", "<p>تم إنشاء تذكرة الدعم <strong>{{TicketNumber}}</strong> (\"{{Subject}}\"). سنرد عليك قريبًا.</p>"),
        [("Support.TicketAssigned", CommunicationChannels.InApp)] = ("أُسندت إليك تذكرة", "أُسندت إليك التذكرة {{TicketNumber}} (\"{{Subject}}\")."),
        [("Support.TicketAssigned", CommunicationChannels.Email)] = ("أُسندت إليك تذكرة الدعم {{TicketNumber}}", "<p>أُسندت إليك تذكرة الدعم <strong>{{TicketNumber}}</strong> (\"{{Subject}}\").</p>"),
        [("Support.TicketStatusChanged", CommunicationChannels.InApp)] = ("تم تحديث حالة التذكرة", "تغيّرت حالة التذكرة {{TicketNumber}} إلى {{Status}}."),
        [("Support.TicketStatusChanged", CommunicationChannels.Email)] = ("تغيّرت حالة تذكرة الدعم {{TicketNumber}} في HAMBOX", "<p>تغيّرت حالة تذكرة الدعم <strong>{{TicketNumber}}</strong> إلى {{Status}}.</p>"),
        [("Support.TicketResolved", CommunicationChannels.InApp)] = ("تم حل التذكرة", "تم وضع علامة \"محلولة\" على التذكرة {{TicketNumber}}. أخبرنا إن احتجت أي شيء آخر."),
        [("Support.TicketResolved", CommunicationChannels.Email)] = ("تم حل تذكرة الدعم {{TicketNumber}} في HAMBOX", "<p>تم وضع علامة \"محلولة\" على تذكرة الدعم <strong>{{TicketNumber}}</strong>. أخبرنا إن احتجت أي شيء آخر.</p>"),
        [("Support.TicketClosed", CommunicationChannels.InApp)] = ("تم إغلاق التذكرة", "تم إغلاق التذكرة {{TicketNumber}}."),
        [("Support.TicketClosed", CommunicationChannels.Email)] = ("تم إغلاق تذكرة الدعم {{TicketNumber}} في HAMBOX", "<p>تم إغلاق تذكرة الدعم <strong>{{TicketNumber}}</strong>.</p>"),
        [("Support.NewReply", CommunicationChannels.InApp)] = ("رد جديد", "لديك رد جديد على التذكرة {{TicketNumber}}."),
        [("Support.NewReply", CommunicationChannels.Email)] = ("رد جديد على تذكرة الدعم {{TicketNumber}} في HAMBOX", "<p>لديك رد جديد على التذكرة <strong>{{TicketNumber}}</strong>.</p>"),
        [("Support.Mention", CommunicationChannels.InApp)] = ("تمت الإشارة إليك", "تمت الإشارة إليك في التذكرة {{TicketNumber}}."),
        [("Support.Mention", CommunicationChannels.Email)] = ("تمت الإشارة إليك في تذكرة دعم HAMBOX", "<p>تمت الإشارة إليك في التذكرة <strong>{{TicketNumber}}</strong>.</p>"),
    };

    /// <summary>The Arabic text to seed alongside a new default template, or nulls when none is defined.</summary>
    public static (string? Subject, string? Body) For(string key, string channel) =>
        Copy.TryGetValue((key, channel), out var copy) ? (copy.Subject, copy.Body) : (null, null);

    public static async Task BackfillAsync(CommunicationDbContext db, CancellationToken cancellationToken = default)
    {
        var keys = Copy.Keys.Select(k => k.Key).Distinct().ToList();
        var templates = await db.CommunicationTemplates
            .Include(t => t.Versions)
            .Where(t => keys.Contains(t.Key))
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var template in templates)
        {
            if (!Copy.TryGetValue((template.Key, template.Channel), out var copy))
            {
                continue;
            }

            var active = template.Versions.FirstOrDefault(v => v.Id == template.ActiveVersionId);
            if (active is null || !string.IsNullOrWhiteSpace(active.BodyAr))
            {
                continue;
            }

            var version = template.CreateDraftVersion(active.SubjectEn, copy.Subject, active.BodyEn, copy.Body, active.VariablesJson);
            // The version has a client-generated key, so EF would otherwise treat it as an existing row (UPDATE → 0 rows).
            db.CommunicationTemplateVersions.Add(version);
            template.PublishVersion(version.Id, "system");
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Translation copy is a nicety: it must never stop the API from starting.
            db.GetService<ILogger<CommunicationDbContext>>().LogWarning(ex, "Could not add the default Arabic template copy.");
            db.ChangeTracker.Clear();
        }
    }
}
