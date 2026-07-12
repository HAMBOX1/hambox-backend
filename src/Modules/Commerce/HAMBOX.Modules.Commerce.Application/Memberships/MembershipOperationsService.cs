using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Memberships;

internal sealed class MembershipOperationsService
{
    private readonly ICommerceDbContext _dbContext;

    public MembershipOperationsService(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<MembershipSubscription> AssignAsync(
        string userId,
        Guid planId,
        bool autoRenew,
        string? performedByUserId,
        CancellationToken cancellationToken)
    {
        var plan = await GetActivePlanAsync(planId, cancellationToken);
        var existing = await _dbContext.MembershipSubscriptions
            .Where(s => s.UserId == userId && s.Status == MembershipSubscriptionStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var sub in existing)
        {
            sub.Cancel();
        }

        var subscription = MembershipSubscription.Create(userId, plan.Id, plan.DurationDays, autoRenew);
        _dbContext.MembershipSubscriptions.Add(subscription);
        _dbContext.MembershipHistories.Add(MembershipHistory.Create(
            subscription.Id, userId, plan.Id, MembershipHistoryAction.Assigned, performedByUserId: performedByUserId));

        if (plan.Price > 0)
        {
            var tx = MembershipTransaction.CreatePending(subscription.Id, userId, plan.Id, plan.Price);
            tx.Complete();
            _dbContext.MembershipTransactions.Add(tx);
        }

        MembershipAuditWriter.Write(_dbContext, plan.Id, subscription.Id, MembershipAuditAction.MembershipAssigned, performedByUserId);
        MembershipNotificationWriter.Notify(_dbContext, userId, "Membership activated", $"Your {plan.Name} membership is now active.", "Membership");
        return subscription;
    }

    public async Task FulfillAfterPaymentAsync(
        MembershipSubscription subscription,
        MembershipPlan plan,
        MembershipCheckoutAction action,
        Guid orderId,
        string? externalPaymentId,
        string performedByUserId,
        CancellationToken cancellationToken)
    {
        MembershipHistoryAction historyAction;
        MembershipAuditAction auditAction;

        switch (action)
        {
            case MembershipCheckoutAction.Subscribe:
                if (subscription.Status == MembershipSubscriptionStatus.PendingPayment)
                {
                    var existing = await _dbContext.MembershipSubscriptions
                        .Where(s => s.UserId == subscription.UserId && s.Status == MembershipSubscriptionStatus.Active)
                        .ToListAsync(cancellationToken);
                    foreach (var sub in existing)
                    {
                        sub.Cancel();
                    }

                    subscription.Activate(plan.DurationDays);
                }

                historyAction = MembershipHistoryAction.Assigned;
                auditAction = MembershipAuditAction.MembershipAssigned;
                break;

            case MembershipCheckoutAction.Renew:
                subscription.Renew(plan.DurationDays);
                historyAction = MembershipHistoryAction.Renewed;
                auditAction = MembershipAuditAction.MembershipRenewed;
                break;

            case MembershipCheckoutAction.Upgrade:
                var previousPlanId = subscription.PlanId;
                subscription.ChangePlan(plan.Id, plan.DurationDays);
                historyAction = MembershipHistoryAction.Upgraded;
                auditAction = MembershipAuditAction.MembershipUpgraded;
                _dbContext.MembershipHistories.Add(MembershipHistory.Create(
                    subscription.Id,
                    subscription.UserId,
                    plan.Id,
                    historyAction,
                    previousPlanId,
                    performedByUserId));
                MembershipAuditWriter.Write(_dbContext, plan.Id, subscription.Id, auditAction, performedByUserId);
                await AddMembershipTransactionAsync(subscription, plan, orderId, externalPaymentId, cancellationToken);
                return;

            case MembershipCheckoutAction.Downgrade:
                var priorPlanId = subscription.PlanId;
                subscription.ChangePlan(plan.Id, plan.DurationDays);
                historyAction = MembershipHistoryAction.Downgraded;
                auditAction = MembershipAuditAction.MembershipDowngraded;
                _dbContext.MembershipHistories.Add(MembershipHistory.Create(
                    subscription.Id,
                    subscription.UserId,
                    plan.Id,
                    historyAction,
                    priorPlanId,
                    performedByUserId));
                MembershipAuditWriter.Write(_dbContext, plan.Id, subscription.Id, auditAction, performedByUserId);
                await AddMembershipTransactionAsync(subscription, plan, orderId, externalPaymentId, cancellationToken);
                return;

            default:
                throw new InvalidOperationException("Unsupported membership checkout action.");
        }

        _dbContext.MembershipHistories.Add(MembershipHistory.Create(
            subscription.Id,
            subscription.UserId,
            plan.Id,
            historyAction,
            performedByUserId: performedByUserId));

        MembershipAuditWriter.Write(_dbContext, plan.Id, subscription.Id, auditAction, performedByUserId);
        await AddMembershipTransactionAsync(subscription, plan, orderId, externalPaymentId, cancellationToken);
    }

    private async Task AddMembershipTransactionAsync(
        MembershipSubscription subscription,
        MembershipPlan plan,
        Guid orderId,
        string? externalPaymentId,
        CancellationToken cancellationToken)
    {
        if (plan.Price <= 0)
        {
            return;
        }

        var tx = MembershipTransaction.CreatePending(subscription.Id, subscription.UserId, plan.Id, plan.Price, orderId);
        tx.Complete(externalPaymentId);
        _dbContext.MembershipTransactions.Add(tx);
        await Task.CompletedTask;
    }

    public async Task<MembershipSubscription> RenewAsync(
        Guid subscriptionId,
        string? performedByUserId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId, cancellationToken);
        var plan = await GetActivePlanAsync(subscription.PlanId, cancellationToken);
        subscription.Renew(plan.DurationDays);

        _dbContext.MembershipHistories.Add(MembershipHistory.Create(
            subscription.Id, subscription.UserId, plan.Id, MembershipHistoryAction.Renewed, performedByUserId: performedByUserId));

        MembershipAuditWriter.Write(_dbContext, plan.Id, subscription.Id, MembershipAuditAction.MembershipRenewed, performedByUserId);
        MembershipNotificationWriter.Notify(_dbContext, subscription.UserId, "Membership renewed", $"Your {plan.Name} membership has been renewed.", "Membership");
        return subscription;
    }

    public async Task<MembershipSubscription> ChangePlanAsync(
        Guid subscriptionId,
        Guid targetPlanId,
        MembershipHistoryAction action,
        string? performedByUserId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId, cancellationToken);
        var previousPlanId = subscription.PlanId;
        var plan = await GetActivePlanAsync(targetPlanId, cancellationToken);
        subscription.ChangePlan(plan.Id, plan.DurationDays);

        _dbContext.MembershipHistories.Add(MembershipHistory.Create(
            subscription.Id,
            subscription.UserId,
            plan.Id,
            action,
            previousPlanId,
            performedByUserId));

        var auditAction = action switch
        {
            MembershipHistoryAction.Upgraded => MembershipAuditAction.MembershipUpgraded,
            MembershipHistoryAction.Downgraded => MembershipAuditAction.MembershipDowngraded,
            _ => MembershipAuditAction.MembershipAssigned,
        };

        MembershipAuditWriter.Write(_dbContext, plan.Id, subscription.Id, auditAction, performedByUserId);
        var title = action == MembershipHistoryAction.Upgraded ? "Membership upgraded" : "Membership changed";
        MembershipNotificationWriter.Notify(_dbContext, subscription.UserId, title, $"You are now on the {plan.Name} plan.", "Membership");
        return subscription;
    }

    public async Task CancelAsync(Guid subscriptionId, string? performedByUserId, CancellationToken cancellationToken)
    {
        var subscription = await GetSubscriptionAsync(subscriptionId, cancellationToken);
        subscription.Cancel();
        _dbContext.MembershipHistories.Add(MembershipHistory.Create(
            subscription.Id, subscription.UserId, subscription.PlanId, MembershipHistoryAction.Cancelled, performedByUserId: performedByUserId));
        MembershipAuditWriter.Write(_dbContext, subscription.PlanId, subscription.Id, MembershipAuditAction.MembershipCancelled, performedByUserId);
        MembershipNotificationWriter.Notify(_dbContext, subscription.UserId, "Membership cancelled", "Your membership has been cancelled.", "Membership");
    }

    private async Task<MembershipPlan> GetActivePlanAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.MembershipPlans
            .Include(p => p.Benefits)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken)
            ?? throw new InvalidOperationException("Membership plan not found.");

        if (plan.Status != MembershipPlanStatus.Active)
        {
            throw new InvalidOperationException("Membership plan is not active.");
        }

        return plan;
    }

    private async Task<MembershipSubscription> GetSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken) =>
        await _dbContext.MembershipSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken)
        ?? throw new InvalidOperationException("Membership subscription not found.");
}
