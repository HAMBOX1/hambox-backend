namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// Public storefront marketing content for the home page (from Platform Settings).
/// </summary>
public sealed record StorefrontContentDto(
    StorefrontHeroDto Hero,
    StorefrontPromoBannerDto PromoBanner,
    StorefrontFlashDealsDto FlashDeals,
    StorefrontFeaturedCollectionsDto FeaturedCollections,
    StorefrontPopularCategoriesDto PopularCategories,
    StorefrontFeaturedProductDto FeaturedProduct,
    IReadOnlyList<StorefrontTrustItemDto> TrustBar,
    StorefrontFooterDto Footer,
    StorefrontSeoDto Seo,
    IReadOnlyList<StorefrontNavLinkDto> NavigationLinks);

public sealed record StorefrontHeroDto(
    string Title,
    string Subtitle,
    string PrimaryButtonText,
    string PrimaryButtonUrl,
    string SecondaryButtonText,
    string SecondaryButtonUrl,
    string BackgroundImageUrl,
    string? OverlayImageUrl,
    double OverlayOpacity,
    string BadgeText,
    string BadgeColor,
    bool Visible);

public sealed record StorefrontPromoBannerDto(
    string Title,
    string Subtitle,
    string ImageUrl,
    string ButtonText,
    string ButtonUrl,
    int CountdownSeconds,
    string? CountdownEndsAtUtc,
    string BackgroundColor,
    bool Visible);

public sealed record StorefrontFlashDealsDto(
    string SectionTitle,
    string SectionSubtitle,
    bool CountdownEnabled,
    string? CountdownDateUtc,
    int MaximumProducts,
    string SortMethod,
    int CountdownSeconds,
    bool Visible);

public sealed record StorefrontFeaturedCollectionsDto(
    string Title,
    string Subtitle,
    int MaximumItems,
    string DisplayStyle,
    bool Visible);

public sealed record StorefrontPopularCategoriesDto(
    string SectionTitle,
    int MaximumCategories,
    string SortOrder,
    bool Visible);

public sealed record StorefrontFeaturedProductDto(
    string? ProductId,
    string Badge,
    string CallToAction,
    bool Visible);

public sealed record StorefrontTrustItemDto(
    string Id,
    string IconUrl,
    string Title,
    string Description,
    int SortOrder,
    bool Visible);

public sealed record StorefrontFooterDto(
    string CompanyName,
    string Copyright,
    string SupportEmail,
    string SupportPhone,
    string Address,
    string WorkingHours,
    string? FacebookUrl,
    string? InstagramUrl,
    string? XUrl,
    string? DiscordUrl,
    string? TelegramUrl,
    string? YouTubeUrl,
    string? TikTokUrl,
    string? WhatsAppUrl);

public sealed record StorefrontNavLinkDto(
    string Id,
    string LabelEn,
    string LabelAr,
    bool Visible);

public sealed record StorefrontSeoDto(
    string DefaultMetaTitle,
    string DefaultMetaDescription,
    string? OpenGraphImageUrl,
    string TwitterCard,
    string CanonicalUrl);
