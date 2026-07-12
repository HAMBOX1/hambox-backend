namespace HAMBOX.Modules.Commerce.Application.Memberships.Models;

public sealed record ResolvedMembershipBenefitDto(
    string Type,
    string Value,
    string DisplayName);

/// <summary>
/// Snapshot of a user's active membership for commerce integrations.
/// </summary>
public sealed record MembershipSnapshot(
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName,
    string? BadgeLabel,
    DateTime ExpiresOnUtc,
    decimal? DiscountPercentage,
    decimal ReferralMultiplier,
    IReadOnlyList<ResolvedMembershipBenefitDto> Benefits,
    IReadOnlyDictionary<string, string> FeatureFlags)
{
    public bool HasActiveMembership => PlanId != Guid.Empty;

    public static MembershipSnapshot None { get; } = new(
        Guid.Empty,
        Guid.Empty,
        "Free",
        null,
        DateTime.UtcNow,
        null,
        1m,
        [],
        new Dictionary<string, string>());
}
