using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Memberships;

/// <summary>
/// Historical record of membership lifecycle events.
/// </summary>
public sealed class MembershipHistory : Entity
{
    private MembershipHistory()
    {
    }

    private MembershipHistory(
        Guid id,
        Guid subscriptionId,
        string userId,
        Guid planId,
        MembershipHistoryAction action,
        Guid? previousPlanId,
        string? performedByUserId,
        string? notes)
        : base(id)
    {
        SubscriptionId = subscriptionId;
        UserId = userId;
        PlanId = planId;
        Action = action;
        PreviousPlanId = previousPlanId;
        PerformedByUserId = performedByUserId;
        Notes = notes;
        OccurredOnUtc = DateTime.UtcNow;
    }

    public Guid SubscriptionId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public Guid PlanId { get; private set; }
    public Guid? PreviousPlanId { get; private set; }
    public MembershipHistoryAction Action { get; private set; }
    public string? PerformedByUserId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }

    public static MembershipHistory Create(
        Guid subscriptionId,
        string userId,
        Guid planId,
        MembershipHistoryAction action,
        Guid? previousPlanId = null,
        string? performedByUserId = null,
        string? notes = null) =>
        new(Guid.NewGuid(), subscriptionId, userId, planId, action, previousPlanId, performedByUserId, notes);
}
