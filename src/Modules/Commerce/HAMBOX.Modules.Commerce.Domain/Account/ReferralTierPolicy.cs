namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// The single source of truth for referral tier names and their lifetime-point thresholds. Previously
/// duplicated independently in <c>ReferralProfile.ResolveTier</c> and the Application layer's dashboard
/// mapper — centralized here so both read the same table and can never drift apart.
/// </summary>
public static class ReferralTierPolicy
{
    public static readonly IReadOnlyList<(string Name, int MinimumPoints)> Tiers =
    [
        ("Starter", 0),
        ("Bronze", 100),
        ("Silver", 500),
        ("Gold", 1000),
    ];

    public static string DefaultTier => Tiers[0].Name;

    public static string ResolveTier(int lifetimePoints)
    {
        var resolved = Tiers[0];

        foreach (var tier in Tiers)
        {
            if (lifetimePoints >= tier.MinimumPoints)
            {
                resolved = tier;
            }
        }

        return resolved.Name;
    }
}
