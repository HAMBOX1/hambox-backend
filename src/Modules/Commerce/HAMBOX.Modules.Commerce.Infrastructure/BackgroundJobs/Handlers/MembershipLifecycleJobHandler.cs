using System.Globalization;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Hourly ELITE housekeeping: moves lapsed subscriptions to Expired (and tells the customer exactly when they ended) and sends
/// one "ends soon" reminder per subscription. The wording is deliberate: ELITE here is prepaid and never charges a card
/// automatically, so the reminder says it will <em>not</em> renew by itself and never implies a charge (AD-03).
/// </summary>
internal sealed class MembershipLifecycleJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext db,
    ICommunicationService communication) : BackgroundJobHandlerBase<string?>(serializer)
{
    /// <summary>How long before the end the reminder goes out.</summary>
    private static readonly TimeSpan ReminderLead = TimeSpan.FromDays(3);

    public override string JobType => OperationalJobTypes.ProcessMembershipLifecycle;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var planNames = await db.MembershipPlans.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var soon = await db.MembershipSubscriptions
            .Where(s => s.Status == MembershipSubscriptionStatus.Active && s.ExpiresOnUtc > now && s.ExpiresOnUtc <= now.Add(ReminderLead) && s.ExpiryReminderSentOnUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var subscription in soon)
        {
            subscription.MarkReminderSent();
            await db.SaveChangesAsync(cancellationToken);
            await SendAsync(subscription, "EliteExpiringSoon", planNames, cancellationToken);
        }

        var lapsed = await db.MembershipSubscriptions
            .Where(s => s.Status == MembershipSubscriptionStatus.Active && s.ExpiresOnUtc <= now)
            .ToListAsync(cancellationToken);
        foreach (var subscription in lapsed)
        {
            subscription.MarkExpired();
            db.MembershipHistories.Add(MembershipHistory.Create(
                subscription.Id, subscription.UserId, subscription.PlanId, MembershipHistoryAction.Expired, notes: "Membership expired automatically."));
            await db.SaveChangesAsync(cancellationToken);
            await SendAsync(subscription, "EliteExpired", planNames, cancellationToken);
        }
    }

    private Task SendAsync(MembershipSubscription subscription, string template, IReadOnlyDictionary<Guid, string> planNames, CancellationToken cancellationToken) =>
        communication.SendAsync(new CommunicationRequest(
            UserId: subscription.UserId,
            TemplateKey: template,
            Category: CommunicationCategory.Membership,
            Variables: new Dictionary<string, string>
            {
                ["PlanName"] = planNames.GetValueOrDefault(subscription.PlanId, "ELITE"),
                ["EndDate"] = subscription.ExpiresOnUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
            },
            RelatedEntityType: "MembershipSubscription",
            RelatedEntityId: subscription.Id.ToString(),
            ActionUrl: "/account/membership"), cancellationToken);
}
