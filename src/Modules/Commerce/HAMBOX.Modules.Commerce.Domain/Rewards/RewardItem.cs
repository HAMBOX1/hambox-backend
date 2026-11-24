using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Rewards;

/// <summary>
/// A product offered in the Stars Shop at a Stars price. Delivery, region and compatibility come from the underlying
/// catalog variant — a reward is a pricing/eligibility wrapper, never a second copy of the product.
/// </summary>
public sealed class RewardItem : AggregateRoot
{
    private RewardItem()
    {
    }

    private RewardItem(Guid id)
        : base(id)
    {
    }

    public Guid ProductId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public int StarsPrice { get; private set; }

    /// <summary>How many more can be redeemed; null = limited only by the real inventory behind the variant.</summary>
    public int? RemainingQuantity { get; private set; }

    /// <summary>Per-customer limit across all time; null = no limit.</summary>
    public int? MaxPerUser { get; private set; }

    /// <summary>Only customers at or above this loyalty rank (by sort order) see and redeem it; null = everyone.</summary>
    public string? MinRankKey { get; private set; }

    /// <summary>Only active ELITE members may redeem it.</summary>
    public bool RequiresElite { get; private set; }

    /// <summary>Offered in the ELITE monthly gift picker (costs members 0 Stars).</summary>
    public bool IsGiftOption { get; private set; }

    /// <summary>
    /// For a coupon reward: the coupon campaign (a Promotion) a fresh single-use code is generated from when the customer
    /// redeems. Product and variant are then <see cref="Guid.Empty"/> — a coupon reward delivers a code, not a product.
    /// </summary>
    public Guid? CouponPromotionId { get; private set; }

    public int CouponValidDays { get; private set; } = 30;

    /// <summary>Shown name of a coupon reward (a product reward uses the product's own name).</summary>
    public string? TitleEn { get; private set; }

    public string? TitleAr { get; private set; }

    /// <summary>A temporary lower Stars price (a Redemption Sale); null when there is none.</summary>
    public int? SaleStarsPrice { get; private set; }

    public DateTimeOffset? SaleStartsOnUtc { get; private set; }

    public DateTimeOffset? SaleEndsOnUtc { get; private set; }

    public bool IsCoupon => CouponPromotionId.HasValue;

    public int PriceAt(DateTimeOffset now) =>
        SaleStarsPrice is { } sale && (SaleStartsOnUtc is null || SaleStartsOnUtc <= now) && (SaleEndsOnUtc is null || SaleEndsOnUtc > now)
            ? Math.Min(sale, StarsPrice)
            : StarsPrice;

    public void SetCoupon(Guid? promotionId, int validDays, string? titleEn, string? titleAr)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(validDays, 1);
        CouponPromotionId = promotionId;
        CouponValidDays = validDays;
        TitleEn = string.IsNullOrWhiteSpace(titleEn) ? null : titleEn.Trim();
        TitleAr = string.IsNullOrWhiteSpace(titleAr) ? null : titleAr.Trim();
    }

    public void SetSale(int? salePrice, DateTimeOffset? startsOnUtc, DateTimeOffset? endsOnUtc)
    {
        if (salePrice is < 1 || (startsOnUtc is { } s && endsOnUtc is { } e && e <= s))
        {
            throw new ArgumentException("A sale needs a price of at least 1 and an end after its start.", nameof(salePrice));
        }

        SaleStarsPrice = salePrice;
        SaleStartsOnUtc = startsOnUtc;
        SaleEndsOnUtc = endsOnUtc;
    }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? StartsOnUtc { get; private set; }

    public DateTimeOffset? EndsOnUtc { get; private set; }

    public static RewardItem Create(
        Guid productId, Guid variantId, int starsPrice, int? remaining, int? maxPerUser, string? minRankKey,
        bool requiresElite, int sortOrder, bool isActive, DateTimeOffset? startsOnUtc, DateTimeOffset? endsOnUtc)
    {
        var item = new RewardItem(Guid.NewGuid()) { ProductId = productId, ProductVariantId = variantId };
        item.Apply(starsPrice, remaining, maxPerUser, minRankKey, requiresElite, sortOrder, isActive, startsOnUtc, endsOnUtc);
        return item;
    }

    public void Apply(
        int starsPrice, int? remaining, int? maxPerUser, string? minRankKey, bool requiresElite, int sortOrder,
        bool isActive, DateTimeOffset? startsOnUtc, DateTimeOffset? endsOnUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(starsPrice, 1);
        if (remaining is < 0 || maxPerUser is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(remaining));
        }

        if (startsOnUtc is { } s && endsOnUtc is { } e && e <= s)
        {
            throw new ArgumentException("The end must be after the start.", nameof(endsOnUtc));
        }

        StarsPrice = starsPrice;
        RemainingQuantity = remaining;
        MaxPerUser = maxPerUser;
        MinRankKey = string.IsNullOrWhiteSpace(minRankKey) ? null : minRankKey.Trim().ToLowerInvariant();
        RequiresElite = requiresElite;
        SortOrder = sortOrder;
        IsActive = isActive;
        StartsOnUtc = startsOnUtc;
        EndsOnUtc = endsOnUtc;
    }

    public void SetGiftOption(bool isGiftOption) => IsGiftOption = isGiftOption;

    public void SetActive(bool isActive) => IsActive = isActive;

    public bool IsLiveAt(DateTimeOffset now) =>
        IsActive && (StartsOnUtc is null || StartsOnUtc <= now) && (EndsOnUtc is null || EndsOnUtc > now);
}
