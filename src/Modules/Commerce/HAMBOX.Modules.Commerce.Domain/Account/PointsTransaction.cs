using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// One row in a customer's Stars ledger (the brief's "Stars" and the code's "points" are the same balance). The
/// balance is never stored — it is derived from the rows — so it can't drift from the history. Rows are appended;
/// a correction is a new row. The only mutations are lifecycle transitions of <see cref="Status"/>
/// (Pending → Available, Reserved → Available/Cancelled), each one explicit on this type.
/// </summary>
public sealed class PointsTransaction : Entity
{
    private PointsTransaction()
    {
    }

    private PointsTransaction(
        Guid id,
        string userId,
        PointsTransactionType type,
        int points,
        Guid? orderId,
        string? orderNumber,
        decimal? eligibleAmountUsd,
        decimal? pointsPerUsd,
        string description,
        string idempotencyKey,
        StarsStatus status = StarsStatus.Available,
        DateTimeOffset? availableOnUtc = null,
        DateTimeOffset? expiresOnUtc = null,
        string? actorUserId = null,
        string? reference = null)
        : base(id)
    {
        UserId = userId;
        Type = type;
        Points = points;
        OrderId = orderId;
        OrderNumber = orderNumber;
        EligibleAmountUsd = eligibleAmountUsd;
        PointsPerUsd = pointsPerUsd;
        Description = description;
        IdempotencyKey = idempotencyKey;
        Status = status;
        AvailableOnUtc = availableOnUtc;
        ExpiresOnUtc = expiresOnUtc;
        ActorUserId = actorUserId;
        Reference = reference;
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public string UserId { get; private set; } = string.Empty;

    public PointsTransactionType Type { get; private set; }

    /// <summary>Signed: positive when credited, negative when debited.</summary>
    public int Points { get; private set; }

    /// <summary>Null for rows not tied to an order (admin adjustments, referral/bonus credits).</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>Denormalized for display, so the history doesn't need to join Orders (same idea as <c>Order.Email</c>).</summary>
    public string? OrderNumber { get; private set; }

    /// <summary>The USD amount the points were calculated from. Snapshot — later settings changes never touch it.</summary>
    public decimal? EligibleAmountUsd { get; private set; }

    /// <summary>The earning rate in force when the points were awarded. Snapshot, like <see cref="EligibleAmountUsd"/>.</summary>
    public decimal? PointsPerUsd { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>Unique per ledger effect (e.g. <c>earn:{orderId}</c>) — the database rejects a second row for the same effect.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public StarsStatus Status { get; private set; } = StarsStatus.Available;

    /// <summary>For a <see cref="StarsStatus.Pending"/> credit: the earliest moment it may become spendable.</summary>
    public DateTimeOffset? AvailableOnUtc { get; private set; }

    /// <summary>When an unspent credit lapses; null means never.</summary>
    public DateTimeOffset? ExpiresOnUtc { get; private set; }

    /// <summary>The staff member behind a manual row (adjustment); null for system rows.</summary>
    public string? ActorUserId { get; private set; }

    /// <summary>Free reference to the source object (referral id, campaign id, reward order id…).</summary>
    public string? Reference { get; private set; }

    /// <summary>When the customer was warned that this credit is about to expire (once).</summary>
    public DateTimeOffset? ExpiryNotifiedOnUtc { get; private set; }

    public void MarkExpiryNotified() => ExpiryNotifiedOnUtc = DateTimeOffset.UtcNow;

    public static PointsTransaction Earn(
        string userId, Guid orderId, string orderNumber, int points, decimal eligibleAmountUsd, decimal pointsPerUsd,
        StarsStatus status = StarsStatus.Available, DateTimeOffset? availableOnUtc = null, DateTimeOffset? expiresOnUtc = null) =>
        new(
            Guid.NewGuid(),
            userId,
            PointsTransactionType.Earned,
            points > 0 ? points : throw new ArgumentOutOfRangeException(nameof(points)),
            orderId,
            orderNumber,
            eligibleAmountUsd,
            pointsPerUsd,
            $"Points earned on order {orderNumber}.",
            EarnKey(orderId),
            status,
            availableOnUtc,
            expiresOnUtc);

    /// <param name="points">How many points to take back, as a positive number.</param>
    /// <param name="cumulativeReversed">Total points reversed on this order once this row exists; makes the key unique per step.</param>
    public static PointsTransaction Reverse(
        string userId, Guid orderId, string orderNumber, int points, int cumulativeReversed, string reason) =>
        new(
            Guid.NewGuid(),
            userId,
            PointsTransactionType.Reversed,
            points > 0 ? -points : throw new ArgumentOutOfRangeException(nameof(points)),
            orderId,
            orderNumber,
            eligibleAmountUsd: null,
            pointsPerUsd: null,
            reason,
            ReverseKey(orderId, cumulativeReversed));

    /// <summary>A credit that isn't an order earn (referral reward, bonus, redemption refund).</summary>
    public static PointsTransaction Credit(
        string userId, PointsTransactionType type, int points, string description, string idempotencyKey,
        StarsStatus status = StarsStatus.Available, DateTimeOffset? availableOnUtc = null, DateTimeOffset? expiresOnUtc = null,
        Guid? orderId = null, string? orderNumber = null, string? reference = null) =>
        new(
            Guid.NewGuid(), userId, type,
            points > 0 ? points : throw new ArgumentOutOfRangeException(nameof(points)),
            orderId, orderNumber, null, null, description, idempotencyKey,
            status, availableOnUtc, expiresOnUtc, reference: reference);

    /// <summary>A debit (redemption, expiry). <paramref name="points"/> is a positive amount to take.</summary>
    public static PointsTransaction Debit(
        string userId, PointsTransactionType type, int points, string description, string idempotencyKey,
        StarsStatus status = StarsStatus.Available, Guid? orderId = null, string? orderNumber = null, string? reference = null) =>
        new(
            Guid.NewGuid(), userId, type,
            points > 0 ? -points : throw new ArgumentOutOfRangeException(nameof(points)),
            orderId, orderNumber, null, null, description, idempotencyKey,
            status, reference: reference);

    /// <summary>Signed manual correction; always carries the actor and a reason.</summary>
    public static PointsTransaction Adjust(string userId, int signedPoints, string reason, string actorUserId) =>
        new(
            Guid.NewGuid(), userId, PointsTransactionType.AdminAdjustment,
            signedPoints != 0 ? signedPoints : throw new ArgumentOutOfRangeException(nameof(signedPoints)),
            null, null, null, null,
            string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException("A reason is required.", nameof(reason)) : reason.Trim(),
            $"adjust:{Guid.NewGuid():N}",
            actorUserId: actorUserId);

    /// <summary>Pending → Available. A no-op on any other status.</summary>
    public bool Release()
    {
        if (Status != StarsStatus.Pending)
        {
            return false;
        }

        Status = StarsStatus.Available;
        AvailableOnUtc = null;
        return true;
    }

    /// <summary>Pending or Reserved → Cancelled (order failed/refunded before release; redemption rolled back).</summary>
    public bool Cancel()
    {
        if (Status is not (StarsStatus.Pending or StarsStatus.Reserved))
        {
            return false;
        }

        Status = StarsStatus.Cancelled;
        return true;
    }

    /// <summary>Reserved → Available: the redemption succeeded, the debit is final.</summary>
    public bool Settle()
    {
        if (Status != StarsStatus.Reserved)
        {
            return false;
        }

        Status = StarsStatus.Available;
        return true;
    }

    public static string EarnKey(Guid orderId) => $"earn:{orderId:N}";

    public static string ReverseKey(Guid orderId, int cumulativeReversed) => $"reverse:{orderId:N}:{cumulativeReversed}";
}
