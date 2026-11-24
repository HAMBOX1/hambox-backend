using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Loyalty;

/// <summary>Append-only record of every rank change (automatic or manual).</summary>
public sealed class LoyaltyRankHistory : Entity
{
    private LoyaltyRankHistory()
    {
    }

    private LoyaltyRankHistory(Guid id)
        : base(id)
    {
    }

    public string UserId { get; private set; } = string.Empty;

    public string? FromRankKey { get; private set; }

    public string ToRankKey { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    /// <summary>The staff member for a manual change; null for automatic ones.</summary>
    public string? ActorUserId { get; private set; }

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static LoyaltyRankHistory Record(string userId, string? from, string to, string reason, string? actorUserId) =>
        new(Guid.NewGuid())
        {
            UserId = userId,
            FromRankKey = from,
            ToRankKey = to,
            Reason = reason,
            ActorUserId = actorUserId,
            OccurredOnUtc = DateTimeOffset.UtcNow,
        };
}
