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
        string referredEmail,
        DateTime? expiresOnUtc)
        : base(id)
    {
        ReferrerUserId = referrerUserId;
        ReferredUserId = referredUserId;
        ReferredEmail = referredEmail;
        PointsEarned = 0;
        Status = ReferralStatus.Pending;
        ExpiresOnUtc = expiresOnUtc;
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
    /// Gets the referred user's email at redemption time — denormalized (same pattern as
    /// <c>Order.Email</c>) so referral history can display "Friend" without a cross-module
    /// Identity join.
    /// </summary>
    public string ReferredEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the points earned from this referral.
    /// </summary>
    public int PointsEarned { get; private set; }

    /// <summary>
    /// Gets the lifecycle status of this referral.
    /// </summary>
    public ReferralStatus Status { get; private set; }

    /// <summary>
    /// Gets the UTC instant after which a still-Pending referral can no longer qualify for a reward.
    /// Null means the referral never expires.
    /// </summary>
    public DateTime? ExpiresOnUtc { get; private set; }

    /// <summary>
    /// Gets the UTC instant this referral was qualified (the referred user's first order completed).
    /// </summary>
    public DateTime? QualifiedOnUtc { get; private set; }

    /// <summary>
    /// Gets the UTC instant points were awarded to the referrer.
    /// </summary>
    public DateTime? RewardedOnUtc { get; private set; }

    /// <summary>
    /// Optimistic concurrency token, bumped on every state transition. A real (not shadow/database-
    /// generated) property — portable across every EF Core provider, unlike a SQL Server
    /// <c>rowversion</c> column — the same pattern ASP.NET Core Identity uses for
    /// <c>ApplicationUser.ConcurrencyStamp</c>. Protects two concurrent order completions (or a
    /// completion racing a refund) from both successfully transitioning this entry.
    /// </summary>
    public Guid ConcurrencyStamp { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Creates a pending referral history entry.
    /// </summary>
    public static ReferralHistoryEntry CreatePending(
        string referrerUserId, string referredUserId, string referredEmail, DateTime? expiresOnUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referrerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referredUserId);

        if (referrerUserId == referredUserId)
        {
            throw new InvalidOperationException("A user cannot refer themselves.");
        }

        return new ReferralHistoryEntry(Guid.NewGuid(), referrerUserId, referredUserId, referredEmail ?? string.Empty, expiresOnUtc);
    }

    /// <summary>
    /// Marks a pending referral as qualified once the referred user's first order completes.
    /// </summary>
    public void MarkQualified()
    {
        if (Status != ReferralStatus.Pending)
        {
            throw new InvalidOperationException($"Only a pending referral can be qualified (current status: {Status}).");
        }

        Status = ReferralStatus.Qualified;
        QualifiedOnUtc = DateTime.UtcNow;
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// Awards points for a qualified referral, completing the lifecycle.
    /// </summary>
    public void AwardPoints(int points)
    {
        if (points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Points must be greater than zero.");
        }

        if (Status != ReferralStatus.Qualified)
        {
            throw new InvalidOperationException($"Only a qualified referral can be rewarded (current status: {Status}).");
        }

        PointsEarned = points;
        Status = ReferralStatus.Rewarded;
        RewardedOnUtc = DateTime.UtcNow;
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// Marks a pending referral as expired — it missed its qualification window.
    /// </summary>
    public void MarkExpired()
    {
        if (Status != ReferralStatus.Pending)
        {
            throw new InvalidOperationException($"Only a pending referral can expire (current status: {Status}).");
        }

        Status = ReferralStatus.Expired;
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// Reverses a qualified or rewarded referral because the qualifying order was refunded or
    /// cancelled. Does not itself claw back points — the caller is responsible for reversing any
    /// points already awarded to the referrer's profile.
    /// </summary>
    public void Reverse()
    {
        if (Status is not (ReferralStatus.Qualified or ReferralStatus.Rewarded))
        {
            throw new InvalidOperationException($"Only a qualified or rewarded referral can be reversed (current status: {Status}).");
        }

        Status = ReferralStatus.Reversed;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
