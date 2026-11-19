namespace HAMBOX.Application.Abstractions;

/// <summary>What the store knows about one customer's activity (orders, spend, membership, points, referral code).</summary>
public sealed record CustomerActivity(
    int OrderCount,
    int PaidOrderCount,
    decimal PaidSpendUsd,
    DateTimeOffset? LastOrderOnUtc,
    string? MembershipPlan,
    string? MembershipStatus,
    DateTime? MembershipExpiresOnUtc,
    int PointsBalance,
    string? ReferralCode,
    string? ReferralTier);

/// <summary>
/// Lets the Identity module's customer list show each customer's store activity without knowing the Commerce schema
/// (Commerce implements it, the same way Identity implements <see cref="IUserDirectory"/> for Commerce).
/// </summary>
public interface ICustomerActivityReader
{
    /// <summary>Activity per user id (ids with no activity are simply absent from the result).</summary>
    Task<IReadOnlyDictionary<string, CustomerActivity>> GetActivityAsync(
        IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>The ids of the users who have an active membership right now.</summary>
    Task<IReadOnlySet<string>> GetActiveMemberUserIdsAsync(CancellationToken cancellationToken = default);
}
