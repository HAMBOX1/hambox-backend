using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Promotions;

/// <summary>
/// A redeemable coupon code linked to a promotion.
/// </summary>
public sealed class CouponCode : Entity
{
    private CouponCode()
    {
    }

    private CouponCode(
        Guid id,
        Guid promotionId,
        string code,
        bool isSingleUse,
        int? maxUses,
        int? perUserMaxUses,
        DateTime? expiresOnUtc)
        : base(id)
    {
        PromotionId = promotionId;
        Code = code;
        IsSingleUse = isSingleUse;
        MaxUses = maxUses;
        PerUserMaxUses = perUserMaxUses;
        ExpiresOnUtc = expiresOnUtc;
    }

    public Guid PromotionId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public bool IsSingleUse { get; private set; }
    public int? MaxUses { get; private set; }
    public int? PerUserMaxUses { get; private set; }
    public int UsedCount { get; private set; }
    public DateTime? ExpiresOnUtc { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static CouponCode Create(
        Guid promotionId,
        string code,
        bool isSingleUse,
        int? maxUses,
        int? perUserMaxUses,
        DateTime? expiresOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new CouponCode(
            Guid.NewGuid(),
            promotionId,
            NormalizeCode(code),
            isSingleUse,
            maxUses,
            perUserMaxUses,
            expiresOnUtc);
    }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public bool IsExpired(DateTime utcNow) => ExpiresOnUtc.HasValue && utcNow > ExpiresOnUtc.Value;

    public bool HasUsesRemaining()
    {
        if (IsSingleUse && UsedCount >= 1)
        {
            return false;
        }

        if (MaxUses.HasValue && UsedCount >= MaxUses.Value)
        {
            return false;
        }

        return true;
    }

    public void Deactivate() => IsActive = false;

    public void RecordUse() => UsedCount++;
}
