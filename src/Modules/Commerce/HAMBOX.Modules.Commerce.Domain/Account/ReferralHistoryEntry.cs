using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// Represents a referral relationship and points earned.
/// </summary>
public sealed class ReferralHistoryEntry : Entity
{
    private ReferralHistoryEntry()
    {
    }

    private ReferralHistoryEntry(
        Guid id,
        string referrerUserId,
        string referredUserId,
        int pointsEarned)
        : base(id)
    {
        ReferrerUserId = referrerUserId;
        ReferredUserId = referredUserId;
        PointsEarned = pointsEarned;
    }

    /// <summary>
    /// Gets the referrer user identifier.
    /// </summary>
    public string ReferrerUserId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the referred user identifier.
    /// </summary>
    public string ReferredUserId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the points earned from this referral.
    /// </summary>
    public int PointsEarned { get; private set; }

    /// <summary>
    /// Creates a pending referral history entry.
    /// </summary>
    public static ReferralHistoryEntry CreatePending(string referrerUserId, string referredUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referrerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referredUserId);

        if (referrerUserId == referredUserId)
        {
            throw new InvalidOperationException("A user cannot refer themselves.");
        }

        return new ReferralHistoryEntry(Guid.NewGuid(), referrerUserId, referredUserId, pointsEarned: 0);
    }

    /// <summary>
    /// Awards points for a completed referral.
    /// </summary>
    public void AwardPoints(int points)
    {
        if (points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Points must be greater than zero.");
        }

        if (PointsEarned > 0)
        {
            throw new InvalidOperationException("Referral points have already been awarded.");
        }

        PointsEarned = points;
    }
}
