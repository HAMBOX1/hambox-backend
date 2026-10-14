using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// What a hosted-gateway payment means for a MEMBERSHIP order (audit R32): on success the subscription is
/// activated / renewed / changed and the order completes; on failure the pending subscription is cancelled.
/// Cart orders are untouched — both methods are no-ops for them.
/// </summary>
internal static class MembershipHostedFinalizer
{
    public static async Task<bool> TryActivateAsync(
        ICommerceDbContext db,
        MembershipOperationsService? membershipOperations,
        Order order,
        string providerTransactionId,
        CancellationToken cancellationToken)
    {
        if (order.Kind != OrderKind.Membership)
        {
            return false;
        }

        if (membershipOperations is null
            || order.MembershipPlanId is not Guid planId
            || order.MembershipSubscriptionId is not Guid subscriptionId
            || order.MembershipAction is not MembershipCheckoutAction action)
        {
            throw new InvalidOperationException($"Membership order {order.Id} cannot be activated: its plan or subscription is missing.");
        }

        var plan = await db.MembershipPlans.Include(p => p.Benefits).FirstOrDefaultAsync(p => p.Id == planId, cancellationToken)
            ?? throw new InvalidOperationException($"Membership plan {planId} no longer exists.");
        var subscription = await db.MembershipSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken)
            ?? throw new InvalidOperationException($"Membership subscription {subscriptionId} no longer exists.");

        await membershipOperations.FulfillAfterPaymentAsync(
            subscription, plan, action, order.Id, providerTransactionId, order.UserId, cancellationToken);

        order.LinkMembershipSubscription(subscription.Id);
        order.Complete();
        return true;
    }

    public static async Task OnPaymentFailedAsync(ICommerceDbContext db, Order order, CancellationToken cancellationToken)
    {
        if (order.Kind != OrderKind.Membership || order.MembershipSubscriptionId is not Guid subscriptionId)
        {
            return;
        }

        var subscription = await db.MembershipSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.Status == MembershipSubscriptionStatus.PendingPayment, cancellationToken);
        subscription?.CancelPendingPayment();
    }
}
