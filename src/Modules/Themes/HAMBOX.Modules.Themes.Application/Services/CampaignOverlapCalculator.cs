using HAMBOX.Modules.Themes.Domain.Campaigns;

namespace HAMBOX.Modules.Themes.Application.Services;

/// <summary>
/// Only one campaign can resolve at a time, so two live campaigns overlapping in time — even
/// with a deterministic winner — is worth flagging to the admin. Shared by the list and detail
/// queries so the "which campaigns overlap" definition lives in exactly one place.
///
/// Takes an already-fetched "all Published+Enabled campaigns" set rather than querying the
/// database itself, so a caller that also needs <see cref="CampaignResolutionOrdering"/>'s
/// current-winner computation (the same candidate set, just additionally window-filtered) can
/// fetch it once and reuse it for both, instead of two near-identical round trips.
/// </summary>
public static class CampaignOverlapCalculator
{
    public static IReadOnlySet<Guid> FindOverlappingIds(
        IReadOnlyList<ThemeCampaign> candidates,
        IReadOnlyList<ThemeCampaign> allLiveCampaigns)
    {
        var relevant = candidates.Where(c => c.Status == CampaignStatus.Published && c.IsEnabled).ToList();
        if (relevant.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var overlapping = new HashSet<Guid>();
        foreach (var campaign in relevant)
        {
            // Half-open interval overlap test: [Start, End) intersects [otherStart, otherEnd).
            var overlapsAnother = allLiveCampaigns.Any(other =>
                other.Id != campaign.Id &&
                campaign.StartsAtUtc < other.EndsAtUtc &&
                other.StartsAtUtc < campaign.EndsAtUtc);

            if (overlapsAnother)
            {
                overlapping.Add(campaign.Id);
            }
        }

        return overlapping;
    }
}
