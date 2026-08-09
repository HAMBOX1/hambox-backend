using HAMBOX.Modules.Themes.Domain.Campaigns;

namespace HAMBOX.Modules.Themes.Application.Services;

/// <summary>
/// The single definition of "which campaign wins when more than one is currently effective" —
/// shared by <see cref="ThemeEngine"/> (the real storefront resolver) and the admin Campaign
/// queries (which need the same answer to distinguish "Live now" from "Active — overridden").
/// Never re-implement this ordering elsewhere; a second, slightly different copy is exactly how
/// the list query's missing <c>CreatedOnUtc</c> tiebreak diverged from the resolver's in a prior
/// audit finding.
/// </summary>
public static class CampaignResolutionOrdering
{
    /// <summary>
    /// Whether a campaign's own date window contains the given instant — the same condition
    /// <see cref="ThemeEngine"/> filters by (in SQL) before applying the tiebreak below.
    /// </summary>
    public static bool IsWithinWindow(ThemeCampaign campaign, DateTime utcNow) =>
        campaign.StartsAtUtc <= utcNow && campaign.EndsAtUtc > utcNow;

    /// <summary>
    /// Priority DESC, then StartsAtUtc DESC, then CreatedOnUtc ASC. Callers are responsible for
    /// pre-filtering to Published + Enabled + within-window candidates first (ThemeEngine does
    /// this in SQL on its hot path; admin queries do it in-memory over an already-fetched set).
    /// </summary>
    public static ThemeCampaign? SelectWinner(IEnumerable<ThemeCampaign> candidates) =>
        candidates
            .OrderByDescending(c => c.Priority)
            .ThenByDescending(c => c.StartsAtUtc)
            .ThenBy(c => c.CreatedOnUtc)
            .FirstOrDefault();
}
