using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Promotions.Models;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Promotions;

internal sealed class PromotionEngine : IPromotionEngine
{
    private const decimal TaxRate = 0.05m;

    private readonly ICommerceDbContext _dbContext;
    private readonly IReadOnlyDictionary<PromotionType, IPromotionTypeEvaluator> _evaluators;
    private readonly IPlatformSettingsProvider _platformSettings;

    public PromotionEngine(
        ICommerceDbContext dbContext,
        IEnumerable<IPromotionTypeEvaluator> evaluators,
        IPlatformSettingsProvider platformSettings)
    {
        _dbContext = dbContext;
        _evaluators = evaluators.ToDictionary(e => e.PromotionType);
        _platformSettings = platformSettings;
    }

    public async Task<PromotionEvaluationResult> EvaluateAsync(
        PromotionEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var lines = context.Lines;
        var subtotal = lines.Sum(l => l.LineTotal);
        var itemCount = lines.Sum(l => l.Quantity);

        if (lines.Count == 0)
        {
            return PromotionEvaluationResult.Empty;
        }

        var promotions = await LoadActivePromotionsAsync(cancellationToken);
        CouponCode? matchedCoupon = null;
        var validationErrors = new List<string>();

        if (!string.IsNullOrWhiteSpace(context.AppliedCouponCode))
        {
            var validation = await ValidateCouponInternalAsync(
                context.AppliedCouponCode,
                context,
                promotions,
                cancellationToken);

            if (validation.IsValid)
            {
                matchedCoupon = validation.Coupon;
            }
            else if (validation.ErrorMessage is not null)
            {
                validationErrors.Add(validation.ErrorMessage);
            }
        }

        var redemptionCounts = await LoadRedemptionCountsAsync(
            promotions.Select(p => p.Id).ToList(),
            context.UserId,
            cancellationToken);

        var request = new PromotionEvaluationRequest(
            lines,
            subtotal,
            context.UserId,
            context.CountryCode,
            context.IsAuthenticated,
            context.IsFirstPurchase,
            context.HasReferralProfile,
            context.AppliedCouponCode,
            matchedCoupon,
            redemptionCounts.UserCounts.GetValueOrDefault(Guid.Empty),
            redemptionCounts.GlobalCounts.GetValueOrDefault(Guid.Empty),
            context.Membership,
            context.UtcNow);

        var automaticCandidates = new List<AppliedPromotionCandidate>();
        AppliedPromotionCandidate? couponCandidate = null;

        foreach (var promotion in promotions)
        {
            if (!_evaluators.TryGetValue(promotion.Type, out var evaluator))
            {
                continue;
            }

            request = request with
            {
                UserRedemptionCount = redemptionCounts.UserCounts.GetValueOrDefault(promotion.Id),
                GlobalRedemptionCount = redemptionCounts.GlobalCounts.GetValueOrDefault(promotion.Id),
                MatchedCoupon = matchedCoupon?.PromotionId == promotion.Id ? matchedCoupon : null,
            };

            var candidate = evaluator.Evaluate(promotion, request);
            if (candidate is null)
            {
                continue;
            }

            if (promotion.Type == PromotionType.CouponCode)
            {
                couponCandidate = candidate;
            }
            else
            {
                automaticCandidates.Add(candidate);
            }
        }

        TryAddMembershipPlanDiscount(context, subtotal, automaticCandidates);

        if (couponCandidate is not null)
        {
            var promotionsSettings = await _platformSettings.GetAsync<PromotionsSettingsPayload>(
                PlatformSettingsCategoryKeys.Promotions, cancellationToken);

            if (!promotionsSettings.StackCouponsAllowed)
            {
                // Coupon stacking disabled: the coupon wins exclusively for this cart rather
                // than combining with automatic/membership/referral discounts.
                automaticCandidates.Clear();
            }
        }

        var applied = automaticCandidates
            .Select(c => ToDto(c))
            .ToList();

        if (couponCandidate is not null)
        {
            applied.Add(ToDto(couponCandidate));
        }

        var totalDiscount = Math.Min(subtotal, applied.Sum(p => p.DiscountAmount));
        var taxableAmount = subtotal - totalDiscount;
        var tax = decimal.Round(Math.Max(0m, taxableAmount) * TaxRate, 2);
        var total = taxableAmount + tax;

        return new PromotionEvaluationResult(
            subtotal,
            totalDiscount,
            tax,
            total,
            itemCount,
            applied,
            validationErrors);
    }

    public async Task<(bool IsValid, string? ErrorMessage, Guid? PromotionId, Guid? CouponCodeId)> ValidateCouponAsync(
        string couponCode,
        PromotionEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var promotions = await LoadActivePromotionsAsync(cancellationToken);
        var result = await ValidateCouponInternalAsync(couponCode, context, promotions, cancellationToken);
        return (result.IsValid, result.ErrorMessage, result.Coupon?.PromotionId, result.Coupon?.Id);
    }

    private async Task<List<Promotion>> LoadActivePromotionsAsync(CancellationToken cancellationToken) =>
        await _dbContext.Promotions
            .Include(p => p.Conditions)
            .Include(p => p.Targets)
            .Include(p => p.CouponCodes)
            .Where(p => p.Status == PromotionStatus.Active && p.IsPublished)
            .ToListAsync(cancellationToken);

    private async Task<(Dictionary<Guid, int> UserCounts, Dictionary<Guid, int> GlobalCounts)> LoadRedemptionCountsAsync(
        IReadOnlyList<Guid> promotionIds,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (promotionIds.Count == 0)
        {
            return ([], []);
        }

        var globalCounts = await _dbContext.PromotionRedemptions
            .Where(r => promotionIds.Contains(r.PromotionId))
            .GroupBy(r => r.PromotionId)
            .Select(g => new { PromotionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PromotionId, x => x.Count, cancellationToken);

        if (userId is null)
        {
            return ([], globalCounts);
        }

        var userCounts = await _dbContext.PromotionRedemptions
            .Where(r => promotionIds.Contains(r.PromotionId) && r.UserId == userId)
            .GroupBy(r => r.PromotionId)
            .Select(g => new { PromotionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PromotionId, x => x.Count, cancellationToken);

        return (userCounts, globalCounts);
    }

    private async Task<(bool IsValid, string? ErrorMessage, CouponCode? Coupon)> ValidateCouponInternalAsync(
        string couponCode,
        PromotionEvaluationContext context,
        IReadOnlyList<Promotion> promotions,
        CancellationToken cancellationToken)
    {
        var normalized = CouponCode.NormalizeCode(couponCode);
        var coupon = await _dbContext.CouponCodes
            .FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);

        if (coupon is null)
        {
            return (false, "Invalid coupon code.", null);
        }

        if (!coupon.IsActive)
        {
            return (false, "This coupon is no longer active.", coupon);
        }

        if (coupon.IsExpired(context.UtcNow))
        {
            return (false, "This coupon has expired.", coupon);
        }

        if (!coupon.HasUsesRemaining())
        {
            return (false, "This coupon has reached its usage limit.", coupon);
        }

        var promotion = promotions.FirstOrDefault(p => p.Id == coupon.PromotionId);
        if (promotion is null)
        {
            return (false, "This coupon is not valid for the current promotion.", coupon);
        }

        if (!promotion.IsActiveAt(context.UtcNow))
        {
            return (false, "This promotion is not currently active.", coupon);
        }

        var subtotal = context.Lines.Sum(l => l.LineTotal);
        var redemptionCounts = await LoadRedemptionCountsAsync([promotion.Id], context.UserId, cancellationToken);
        var request = new PromotionEvaluationRequest(
            context.Lines,
            subtotal,
            context.UserId,
            context.CountryCode,
            context.IsAuthenticated,
            context.IsFirstPurchase,
            context.HasReferralProfile,
            normalized,
            coupon,
            redemptionCounts.UserCounts.GetValueOrDefault(promotion.Id),
            redemptionCounts.GlobalCounts.GetValueOrDefault(promotion.Id),
            context.Membership,
            context.UtcNow);

        var ineligibility = PromotionEligibilityChecker.GetIneligibilityReason(promotion, request);
        if (ineligibility is not null)
        {
            return (false, ineligibility, coupon);
        }

        if (coupon.PerUserMaxUses.HasValue && context.UserId is not null)
        {
            var userCouponUses = await _dbContext.PromotionRedemptions
                .CountAsync(
                    r => r.CouponCodeId == coupon.Id && r.UserId == context.UserId,
                    cancellationToken);

            if (userCouponUses >= coupon.PerUserMaxUses.Value)
            {
                return (false, "You have already used this coupon the maximum number of times.", coupon);
            }
        }

        return (true, null, coupon);
    }

    private static void TryAddMembershipPlanDiscount(
        PromotionEvaluationContext context,
        decimal subtotal,
        List<AppliedPromotionCandidate> automaticCandidates)
    {
        var discountPct = context.Membership.DiscountPercentage;
        if (discountPct is null or <= 0 ||
            automaticCandidates.Any(c => c.Type == PromotionType.Membership))
        {
            return;
        }

        var discount = decimal.Round(subtotal * (discountPct.Value / 100m), 2);
        if (discount <= 0)
        {
            return;
        }

        automaticCandidates.Add(new AppliedPromotionCandidate(
            context.Membership.PlanId,
            null,
            $"{context.Membership.PlanName} Discount",
            PromotionType.Membership,
            null,
            discount,
            IsAutomatic: true,
            $"{discountPct.Value:0.##}% membership discount"));
    }

    private static AppliedPromotionDto ToDto(AppliedPromotionCandidate candidate) =>
        new(
            candidate.PromotionId,
            candidate.CouponCodeId,
            candidate.Name,
            candidate.Type,
            candidate.CouponCode,
            candidate.DiscountAmount,
            candidate.IsAutomatic,
            candidate.Description);
}
