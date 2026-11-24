using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Memberships;

/// <summary>
/// A user's active or historical membership subscription to a plan.
/// </summary>
public sealed class MembershipSubscription : AggregateRoot
{
    private MembershipSubscription()
    {
    }

    private MembershipSubscription(
        Guid id,
        string userId,
        Guid planId,
        DateTime startsOnUtc,
        DateTime expiresOnUtc,
        bool autoRenew,
        MembershipSubscriptionStatus status)
        : base(id)
    {
        UserId = userId;
        PlanId = planId;
        StartsOnUtc = startsOnUtc;
        ExpiresOnUtc = expiresOnUtc;
        AutoRenew = autoRenew;
        Status = status;
    }

    public string UserId { get; private set; } = string.Empty;
    public Guid PlanId { get; private set; }
    public MembershipSubscriptionStatus Status { get; private set; }
    public DateTime StartsOnUtc { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime? CancelledOnUtc { get; private set; }
    public bool AutoRenew { get; private set; }

    /// <summary>When the "ends soon" reminder went out for the current period; cleared on renewal so the next period gets its own.</summary>
    public DateTime? ExpiryReminderSentOnUtc { get; private set; }

    public void MarkReminderSent() => ExpiryReminderSentOnUtc = DateTime.UtcNow;

    public static MembershipSubscription Create(
        string userId,
        Guid planId,
        int durationDays,
        bool autoRenew = false,
        int durationMonths = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var now = DateTime.UtcNow;
        return new MembershipSubscription(
            Guid.NewGuid(),
            userId,
            planId,
            now,
            Advance(now, durationDays, durationMonths),
            autoRenew,
            MembershipSubscriptionStatus.Active);
    }

    public static MembershipSubscription CreatePendingPayment(
        string userId,
        Guid planId,
        bool autoRenew = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var now = DateTime.UtcNow;
        return new MembershipSubscription(
            Guid.NewGuid(),
            userId,
            planId,
            now,
            now,
            autoRenew,
            MembershipSubscriptionStatus.PendingPayment);
    }

    public void Activate(int durationDays, int durationMonths = 0)
    {
        if (Status is MembershipSubscriptionStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled subscriptions cannot be activated.");
        }

        var now = DateTime.UtcNow;
        StartsOnUtc = now;
        ExpiresOnUtc = Advance(now, durationDays, durationMonths);
        ExpiryReminderSentOnUtc = null;
        Status = MembershipSubscriptionStatus.Active;
        CancelledOnUtc = null;
    }

    public void CancelPendingPayment()
    {
        if (Status != MembershipSubscriptionStatus.PendingPayment)
        {
            throw new InvalidOperationException("Only pending payment subscriptions can be cancelled.");
        }

        Status = MembershipSubscriptionStatus.Cancelled;
        CancelledOnUtc = DateTime.UtcNow;
        AutoRenew = false;
    }

    public void Renew(int durationDays, int durationMonths = 0)
    {
        if (Status == MembershipSubscriptionStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled subscriptions cannot be renewed.");
        }

        var baseDate = ExpiresOnUtc < DateTime.UtcNow ? DateTime.UtcNow : ExpiresOnUtc;
        ExpiresOnUtc = Advance(baseDate, durationDays, durationMonths);
        ExpiryReminderSentOnUtc = null;
        Status = MembershipSubscriptionStatus.Active;
        CancelledOnUtc = null;
    }

    public void ChangePlan(Guid newPlanId, int durationDays, int durationMonths = 0)
    {
        PlanId = newPlanId;
        Renew(durationDays, durationMonths);
    }

    public void Cancel()
    {
        Status = MembershipSubscriptionStatus.Cancelled;
        CancelledOnUtc = DateTime.UtcNow;
        AutoRenew = false;
    }

    public void MarkExpired()
    {
        if (Status == MembershipSubscriptionStatus.Active && ExpiresOnUtc <= DateTime.UtcNow)
        {
            Status = MembershipSubscriptionStatus.Expired;
        }
    }

    private static DateTime Advance(DateTime from, int days, int months) => months > 0 ? from.AddMonths(months) : from.AddDays(days);

    public bool IsActiveAt(DateTime utcNow) =>
        Status == MembershipSubscriptionStatus.Active && utcNow < ExpiresOnUtc;
}
