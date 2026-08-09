namespace HAMBOX.Modules.Themes.Application.Contracts.Campaigns;

public sealed record CampaignListItemDto(
    Guid Id,
    string Name,
    Guid ThemeId,
    string ThemeName,
    string ThemeStatus,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int Priority,
    string Status,
    bool IsEnabled,
    string Phase,
    bool HasOverlap,
    // True only for the single campaign ThemeEngine would actually pick right now (computed via
    // the same CampaignResolutionOrdering the resolver uses). Phase alone can say "Active" for
    // more than one overlapping campaign; this is the tiebroken answer to "which one is really live".
    bool IsResolvedWinner,
    // Set only when Phase is Active but IsResolvedWinner is false — names the campaign that is
    // actually driving the storefront theme instead.
    string? OverriddenByCampaignName,
    DateTimeOffset CreatedOnUtc);

public sealed record CampaignDetailDto(
    Guid Id,
    string Name,
    string? Description,
    Guid ThemeId,
    string ThemeName,
    string ThemeStatus,
    bool ThemeIsPublishable,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int Priority,
    string Status,
    bool IsEnabled,
    string Phase,
    bool HasOverlap,
    bool IsResolvedWinner,
    string? OverriddenByCampaignName,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc);

public sealed record CreateCampaignRequest(
    string Name,
    string? Description,
    Guid ThemeId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int Priority = 0);

public sealed record UpdateCampaignRequest(
    string Name,
    string? Description,
    Guid ThemeId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int Priority);

public sealed record CampaignHistoryEntryDto(
    Guid Id,
    string Action,
    string? ActorUserId,
    DateTime CreatedOnUtc,
    string? DetailsJson);
