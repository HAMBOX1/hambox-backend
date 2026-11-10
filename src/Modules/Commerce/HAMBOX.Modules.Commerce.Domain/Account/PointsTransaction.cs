using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// One immutable row in a customer's purchase-points ledger. The balance is never stored — it is the sum of
/// <see cref="Points"/> — so it can't drift from the history. Rows are only ever appended: a correction is a
/// new <see cref="PointsTransactionType.Reversed"/> row, never an edit or a delete.
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
        Guid orderId,
        string orderNumber,
        decimal? eligibleAmountUsd,
        decimal? pointsPerUsd,
        string description,
        string idempotencyKey)
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
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public string UserId { get; private set; } = string.Empty;

    public PointsTransactionType Type { get; private set; }

    /// <summary>Signed: positive when earned, negative when reversed.</summary>
    public int Points { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>Denormalized for display, so the history doesn't need to join Orders (same idea as <c>Order.Email</c>).</summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>The USD amount the points were calculated from. Snapshot — later settings changes never touch it.</summary>
    public decimal? EligibleAmountUsd { get; private set; }

    /// <summary>The earning rate in force when the points were awarded. Snapshot, like <see cref="EligibleAmountUsd"/>.</summary>
    public decimal? PointsPerUsd { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>Unique per ledger effect (e.g. <c>earn:{orderId}</c>) — the database rejects a second row for the same effect.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static PointsTransaction Earn(
        string userId, Guid orderId, string orderNumber, int points, decimal eligibleAmountUsd, decimal pointsPerUsd) =>
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
            EarnKey(orderId));

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

    public static string EarnKey(Guid orderId) => $"earn:{orderId:N}";

    public static string ReverseKey(Guid orderId, int cumulativeReversed) => $"reverse:{orderId:N}:{cumulativeReversed}";
}
