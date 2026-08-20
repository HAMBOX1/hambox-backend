using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Promotions.Models;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Redeems a previously-evaluated set of applied promotions/coupons against a specific order:
/// records <see cref="OrderAppliedPromotion"/>/<see cref="PromotionRedemption"/> and atomically
/// increments usage counters. Extracted out of the original single synchronous
/// <c>CheckoutCommandHandler</c> so a deferred-settlement checkout (DOT) can redeem at
/// finalization time — after payment is confirmed — instead of at cart-evaluation time, without
/// duplicating the usage-limit-guarded update logic.
/// <para>
/// Must run inside the same <see cref="ICommerceTransactionService"/> transaction as the order's
/// completion. Throws <see cref="InvalidOperationException"/> on a usage-limit race loss, matching
/// the original handler's signaling so callers can reuse the same catch/translate logic.
/// </para>
/// </summary>
public sealed class PromotionRedemptionService(ICommerceDbContext commerceDbContext)
{
    public async Task RedeemAsync(
        Order order,
        IReadOnlyList<AppliedPromotionDto> appliedPromotions,
        string? userId,
        CancellationToken cancellationToken)
    {
        foreach (var applied in appliedPromotions)
        {
            commerceDbContext.OrderAppliedPromotions.Add(OrderAppliedPromotion.Create(
                order.Id,
                applied.PromotionId,
                applied.CouponCodeId,
                applied.Name,
                applied.Type,
                applied.DiscountAmount,
                applied.CouponCode));

            commerceDbContext.PromotionRedemptions.Add(PromotionRedemption.Create(
                applied.PromotionId,
                applied.CouponCodeId,
                userId,
                order.Id,
                applied.DiscountAmount,
                applied.Name));

            var promotion = await commerceDbContext.Promotions
                .Include(p => p.Conditions)
                .FirstOrDefaultAsync(p => p.Id == applied.PromotionId, cancellationToken);

            if (promotion is not null)
            {
                var promotionId = promotion.Id;
                var usageLimit = promotion.GetConditionInt(PromotionConditionType.UsageLimit);
                var perUserLimit = promotion.GetConditionInt(PromotionConditionType.PerUserLimit);
                var redeemingUserId = userId!;

                // Atomic, condition-guarded UPDATE: the row lock on the matched Promotion row
                // serializes concurrent redemptions against it, so one that would exceed
                // UsageLimit/PerUserLimit blocks until the earlier transaction commits, then
                // re-evaluates the WHERE clause and affects 0 rows instead of over-redeeming.
                var promotionUpdated = await commerceDbContext.Promotions
                    .Where(p => p.Id == promotionId
                        && (usageLimit == null || p.TotalRedemptions < usageLimit.Value)
                        && (perUserLimit == null || commerceDbContext.PromotionRedemptions
                            .Count(r => r.PromotionId == promotionId && r.UserId == redeemingUserId) < perUserLimit.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.TotalRedemptions, p => p.TotalRedemptions + 1), cancellationToken);

                if (promotionUpdated == 0)
                {
                    throw new InvalidOperationException("Promotion usage limit reached.");
                }
            }

            if (applied.CouponCodeId is not null)
            {
                var couponId = applied.CouponCodeId.Value;

                // Same atomic, condition-guarded UPDATE pattern as promotion redemption above.
                var couponUpdated = await commerceDbContext.CouponCodes
                    .Where(c => c.Id == couponId
                        && c.IsActive
                        && (c.MaxUses == null || c.UsedCount < c.MaxUses.Value)
                        && (!c.IsSingleUse || c.UsedCount == 0)
                        && (c.PerUserMaxUses == null || commerceDbContext.PromotionRedemptions
                            .Count(r => r.CouponCodeId == couponId && r.UserId == userId) < c.PerUserMaxUses.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount + 1), cancellationToken);

                if (couponUpdated == 0)
                {
                    throw new InvalidOperationException("Coupon usage limit reached.");
                }

                commerceDbContext.PromotionAuditLogs.Add(PromotionAuditLog.Create(
                    applied.PromotionId,
                    applied.CouponCodeId,
                    PromotionAuditAction.CouponRedeemed,
                    userId,
                    $"Coupon {applied.CouponCode} redeemed on order {order.OrderNumber}"));
            }
        }
    }
}
