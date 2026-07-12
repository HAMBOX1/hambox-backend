using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Memberships;

/// <summary>
/// Audit trail for membership administration.
/// </summary>
public sealed class MembershipAuditLog : Entity
{
    private MembershipAuditLog()
    {
    }

    private MembershipAuditLog(
        Guid id,
        Guid? planId,
        Guid? subscriptionId,
        MembershipAuditAction action,
        string? performedByUserId,
        string? details)
        : base(id)
    {
        PlanId = planId;
        SubscriptionId = subscriptionId;
        Action = action;
        PerformedByUserId = performedByUserId;
        Details = details;
        OccurredOnUtc = DateTime.UtcNow;
    }

    public Guid? PlanId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public MembershipAuditAction Action { get; private set; }
    public string? PerformedByUserId { get; private set; }
    public string? Details { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }

    public static MembershipAuditLog Create(
        Guid? planId,
        Guid? subscriptionId,
        MembershipAuditAction action,
        string? performedByUserId,
        string? details = null) =>
        new(Guid.NewGuid(), planId, subscriptionId, action, performedByUserId, details);
}
