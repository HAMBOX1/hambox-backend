using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Loyalty;

/// <summary>A customer's current rank. One row per user; absence means <see cref="LoyaltyRankKeys.Normal"/>.</summary>
public sealed class CustomerLoyaltyRank : Entity
{
    private CustomerLoyaltyRank()
    {
    }

    private CustomerLoyaltyRank(Guid id, string userId)
        : base(id)
    {
        UserId = userId;
    }

    public string UserId { get; private set; } = string.Empty;

    public string RankKey { get; private set; } = LoyaltyRankKeys.Normal;

    /// <summary>When the customer reached the current rank — kept even if they later spend Stars.</summary>
    public DateTimeOffset AchievedOnUtc { get; private set; }

    /// <summary>A staff-set rank is pinned: automatic evaluation leaves it alone until an admin clears it.</summary>
    public bool IsManual { get; private set; }

    public static CustomerLoyaltyRank Create(string userId) => new(Guid.NewGuid(), userId) { AchievedOnUtc = DateTimeOffset.UtcNow };

    public void MoveTo(string rankKey, bool isManual)
    {
        RankKey = rankKey;
        IsManual = isManual;
        AchievedOnUtc = DateTimeOffset.UtcNow;
    }
}
