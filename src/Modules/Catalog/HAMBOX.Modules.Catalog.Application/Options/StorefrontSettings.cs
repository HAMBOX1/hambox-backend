namespace HAMBOX.Modules.Catalog.Application.Options;

/// <summary>
/// Storefront marketing content served to the public home page.
/// </summary>
public sealed class StorefrontSettings
{
    public const string SectionName = "Storefront";

    public StorefrontHeroSettings Hero { get; init; } = new();

    public StorefrontPromoBannerSettings PromoBanner { get; init; } = new();

    public int FlashDealsCountdownSeconds { get; init; } = 15_735;
}

public sealed class StorefrontHeroSettings
{
    public string Eyebrow { get; init; } = "NEW SEASON LIVE";

    public string TitleLine1 { get; init; } = "The Next Level of Digital";

    public string TitleAccent { get; init; } = "Gaming";

    public string Description { get; init; } =
        "Unlock thousands of titles with instant digital delivery. Experience the safest marketplace for gamers worldwide.";

    public string BackgroundImageUrl { get; init; } = "/assets/images/hambox-hero-background.png";

    public string OverlayImageUrl { get; init; } = "/assets/images/hambox-hero-overlay.png";

    public string PrimaryCtaLabel { get; init; } = "Shop Now";

    public string PrimaryCtaRoute { get; init; } = "/products";

    public string SecondaryCtaLabel { get; init; } = "View Deals";

    public string SecondaryCtaRoute { get; init; } = "/products";
}

public sealed class StorefrontPromoBannerSettings
{
    public string Headline { get; init; } = "Cyberpunk Sale Event";

    public string Subheadline { get; init; } =
        "Up to 80% off premium digital titles. Instant global delivery.";

    public string BackgroundImageUrl { get; init; } = "/assets/images/hambox-hero-background.png";

    public int CountdownSeconds { get; init; } = 15_735;
}
