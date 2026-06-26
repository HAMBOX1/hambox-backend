namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// Public storefront marketing content for the home page.
/// </summary>
public sealed record StorefrontContentDto(
    StorefrontHeroDto Hero,
    StorefrontPromoBannerDto PromoBanner,
    int FlashDealsCountdownSeconds);

public sealed record StorefrontHeroDto(
    string Eyebrow,
    string TitleLine1,
    string TitleAccent,
    string Description,
    string BackgroundImageUrl,
    string OverlayImageUrl,
    string PrimaryCtaLabel,
    string PrimaryCtaRoute,
    string SecondaryCtaLabel,
    string SecondaryCtaRoute);

public sealed record StorefrontPromoBannerDto(
    string Headline,
    string Subheadline,
    string BackgroundImageUrl,
    int CountdownSeconds);
