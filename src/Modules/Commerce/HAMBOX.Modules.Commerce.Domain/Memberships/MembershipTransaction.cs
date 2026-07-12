using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Memberships;

/// <summary>
/// Payment record for a membership (future recurring billing ready).
/// </summary>
public sealed class MembershipTransaction : Entity
{
    private MembershipTransaction()
    {
    }

    private MembershipTransaction(
        Guid id,
        Guid subscriptionId,
        string userId,
        Guid planId,
        decimal amount,
        MembershipTransactionStatus status,
        string? externalPaymentId,
        Guid? orderId)
        : base(id)
    {
        SubscriptionId = subscriptionId;
        UserId = userId;
        PlanId = planId;
        Amount = amount;
        Status = status;
        ExternalPaymentId = externalPaymentId;
        OrderId = orderId;
    }

    public Guid? OrderId { get; private set; }

    public Guid SubscriptionId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public Guid PlanId { get; private set; }
    public decimal Amount { get; private set; }
    public MembershipTransactionStatus Status { get; private set; }
    public string? ExternalPaymentId { get; private set; }

    public static MembershipTransaction CreatePending(
        Guid subscriptionId,
        string userId,
        Guid planId,
        decimal amount,
        Guid? orderId = null) =>
        new(Guid.NewGuid(), subscriptionId, userId, planId, amount, MembershipTransactionStatus.Pending, null, orderId);

    public void LinkOrder(Guid orderId) => OrderId = orderId;

    public void Complete(string? externalPaymentId = null)
    {
        Status = MembershipTransactionStatus.Completed;
        ExternalPaymentId = externalPaymentId;
    }
}
