namespace HAMBOX.Application.Membership;

/// <summary>
/// Read-only view of what a user's active (or default) membership plan actually grants.
/// Backed by the Commerce module's membership engine; exposed here so other modules can
/// enforce membership benefits without taking a hard dependency on Commerce.
/// </summary>
public sealed record MembershipAccessInfo(
    Guid PlanId,
    string PlanName,
    string PlanSlug,
    bool HasActiveMembership,
    IReadOnlyCollection<string> FeatureFlags,
    int? MaxPurchasesPerMonth,
    int EarlyAccessDays,
    bool HasPrioritySupport,
    /// <summary>Null when the caller has no explicit subscription and is only on the plan's
    /// admin-configured default (e.g. a free tier) — distinguishes "genuinely subscribed" from
    /// "falls back to the default plan" for consumers that display subscription details.</summary>
    Guid? SubscriptionId,
    /// <summary>Null unless <see cref="SubscriptionId"/> is set — never a synthetic/fallback date.</summary>
    DateTime? ExpiresOnUtc)
{
    public static readonly MembershipAccessInfo None =
        new(Guid.Empty, string.Empty, string.Empty, false, [], null, 0, false, null, null);

    public bool HasFeature(string key) => FeatureFlags.Contains(key, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Whether a given product is restricted to certain membership plans, and if so whether the viewer qualifies.</summary>
public sealed record ProductAccessInfo(
    bool IsRestricted,
    bool HasAccess,
    IReadOnlyList<string> RequiredPlanNames)
{
    public static readonly ProductAccessInfo Unrestricted = new(false, true, []);
}

public interface IMembershipAccessProvider
{
    /// <summary>Resolves the caller's effective membership (their active subscription, or the plan's admin-configured default if none).</summary>
    Task<MembershipAccessInfo> GetAccessInfoAsync(string? userId, CancellationToken cancellationToken = default);

    /// <summary>Whether the given user currently has purchase access to a single product.</summary>
    Task<ProductAccessInfo> GetProductAccessAsync(string? userId, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Batch form of <see cref="GetProductAccessAsync"/> for listing pages — avoids N+1 lookups.</summary>
    Task<IReadOnlyDictionary<Guid, ProductAccessInfo>> GetProductsAccessAsync(
        string? userId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);
}
