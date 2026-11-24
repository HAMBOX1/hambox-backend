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
    string? ReferralTier,
    int StarsPending = 0,
    string? RankKey = null);

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

    /// <summary>The ids of the users whose loyalty rank is <paramref name="rankKey"/> (empty when ranks are not in use).</summary>
    Task<IReadOnlySet<string>> GetUserIdsByRankAsync(string rankKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>The full Customer 360 picture of one customer's store-side history.</summary>
    Task<CustomerStoreProfile> GetProfileAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerStoreProfile.Empty);
}

public sealed record CustomerOrderSummary(Guid Id, string OrderNumber, string Status, string PaymentStatus, decimal TotalUsd, string PaymentMethod, DateTimeOffset CreatedOnUtc);

public sealed record CustomerLedgerEntry(string Type, int Points, string Status, string Description, DateTimeOffset OccurredOnUtc);

public sealed record CustomerRankEvent(string? From, string To, string Reason, DateTimeOffset OccurredOnUtc);

public sealed record CustomerSubscriptionSummary(string PlanName, string Status, DateTime StartsOnUtc, DateTime ExpiresOnUtc);

public sealed record CustomerLegacyRecord(string LegacyId, string? Name, int PromisedStars, string Status, DateTimeOffset? LinkedOnUtc, int OrderCount);

public sealed record CustomerStoreProfile(
    string? RankKey,
    int StarsAvailable,
    int StarsPending,
    int StarsLifetime,
    IReadOnlyList<CustomerOrderSummary> RecentOrders,
    IReadOnlyList<CustomerLedgerEntry> RecentLedger,
    IReadOnlyList<CustomerRankEvent> RankHistory,
    IReadOnlyList<CustomerSubscriptionSummary> Subscriptions,
    int ReferralsMade,
    int ReferralsRewarded,
    IReadOnlyList<CustomerLegacyRecord> LegacyRecords)
{
    public static CustomerStoreProfile Empty { get; } = new(null, 0, 0, 0, [], [], [], [], 0, 0, []);
}
