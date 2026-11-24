using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Rewards;

public enum RewardRedemptionSource
{
    StarsShop = 0,
    EliteMonthlyGift = 1,
}

public enum RewardRedemptionStatus
{
    Processing = 0,
    Completed = 1,
    Failed = 2,
}

/// <summary>One redemption of a reward: who, what, how many Stars, and the ordinary order it produced.</summary>
public sealed class RewardRedemption : Entity
{
    private RewardRedemption()
    {
    }

    private RewardRedemption(Guid id)
        : base(id)
    {
    }

    public string UserId { get; private set; } = string.Empty;

    public Guid RewardItemId { get; private set; }

    public Guid OrderId { get; private set; }

    public int StarsSpent { get; private set; }

    public RewardRedemptionSource Source { get; private set; }

    public RewardRedemptionStatus Status { get; private set; }

    /// <summary>For the monthly gift: the period it was claimed for (e.g. "2026-11"); null otherwise.</summary>
    public string? PeriodKey { get; private set; }

    public DateTimeOffset RedeemedOnUtc { get; private set; }

    /// <summary>For a coupon reward: the code the customer received (the order is then empty).</summary>
    public string? CouponCode { get; private set; }

    public void SetCouponCode(string code)
    {
        CouponCode = code;
        Status = RewardRedemptionStatus.Completed;
    }

    public static RewardRedemption Create(
        string userId, Guid rewardItemId, Guid orderId, int starsSpent, RewardRedemptionSource source, string? periodKey) =>
        new(Guid.NewGuid())
        {
            UserId = userId,
            RewardItemId = rewardItemId,
            OrderId = orderId,
            StarsSpent = starsSpent,
            Source = source,
            Status = RewardRedemptionStatus.Processing,
            PeriodKey = periodKey,
            RedeemedOnUtc = DateTimeOffset.UtcNow,
        };

    public void MarkCompleted() => Status = RewardRedemptionStatus.Completed;

    public void MarkFailed() => Status = RewardRedemptionStatus.Failed;
}

/// <summary>A reward a customer is saving towards. Not a reservation — it never holds stock or Stars.</summary>
public sealed class StarsGoal : Entity
{
    private StarsGoal()
    {
    }

    private StarsGoal(Guid id)
        : base(id)
    {
    }

    public string UserId { get; private set; } = string.Empty;

    public Guid RewardItemId { get; private set; }

    public static StarsGoal Create(string userId, Guid rewardItemId) => new(Guid.NewGuid()) { UserId = userId, RewardItemId = rewardItemId };
}
