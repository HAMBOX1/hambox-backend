using HAMBOX.Modules.Themes.Application.Contracts.Campaigns;
using HAMBOX.Modules.Themes.Domain.Campaigns;
using HAMBOX.Modules.Themes.Domain.Themes;

namespace HAMBOX.Modules.Themes.Application.Services;

public static class CampaignMapper
{
    public static CampaignListItemDto ToListItem(
        ThemeCampaign campaign,
        StoreTheme? theme,
        DateTime utcNow,
        bool hasOverlap,
        ThemeCampaign? currentWinner)
    {
        var phase = campaign.GetPhase(utcNow);
        var isResolvedWinner = currentWinner is not null && currentWinner.Id == campaign.Id;
        var overriddenByCampaignName = phase == CampaignPhase.Active && !isResolvedWinner ? currentWinner?.Name : null;

        return new(
            campaign.Id,
            campaign.Name,
            campaign.ThemeId,
            theme?.Name ?? "(theme deleted)",
            theme?.Status.ToString() ?? "Unknown",
            campaign.StartsAtUtc,
            campaign.EndsAtUtc,
            campaign.Priority,
            campaign.Status.ToString(),
            campaign.IsEnabled,
            phase.ToString(),
            hasOverlap,
            isResolvedWinner,
            overriddenByCampaignName,
            campaign.CreatedOnUtc);
    }

    public static CampaignDetailDto ToDetail(
        ThemeCampaign campaign,
        StoreTheme? theme,
        DateTime utcNow,
        bool hasOverlap,
        ThemeCampaign? currentWinner)
    {
        var phase = campaign.GetPhase(utcNow);
        var isResolvedWinner = currentWinner is not null && currentWinner.Id == campaign.Id;
        var overriddenByCampaignName = phase == CampaignPhase.Active && !isResolvedWinner ? currentWinner?.Name : null;

        return new(
            campaign.Id,
            campaign.Name,
            campaign.Description,
            campaign.ThemeId,
            theme?.Name ?? "(theme deleted)",
            theme?.Status.ToString() ?? "Unknown",
            IsThemePublishable(theme),
            campaign.StartsAtUtc,
            campaign.EndsAtUtc,
            campaign.Priority,
            campaign.Status.ToString(),
            campaign.IsEnabled,
            phase.ToString(),
            hasOverlap,
            isResolvedWinner,
            overriddenByCampaignName,
            campaign.CreatedOnUtc,
            campaign.ModifiedOnUtc);
    }

    // StoreThemes is always queried through the DbSet's own global soft-delete filter, so a
    // non-null theme here can never be IsDeleted — only its Status needs checking.
    public static bool IsThemePublishable(StoreTheme? theme) =>
        theme is not null && theme.Status == StoreThemeStatus.Published;
}
