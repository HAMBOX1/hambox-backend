using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Storefront.GetStorefrontContent;

internal sealed class GetStorefrontContentQueryHandler(IPlatformSettingsProvider platformSettings)
    : IRequestHandler<GetStorefrontContentQuery, Result<StorefrontContentDto>>
{
    public async Task<Result<StorefrontContentDto>> Handle(
        GetStorefrontContentQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await platformSettings.GetStorefrontAsync(cancellationToken);

        var content = new StorefrontContentDto(
            new StorefrontHeroDto(
                settings.Hero.Title,
                settings.Hero.Subtitle,
                settings.Hero.PrimaryButtonText,
                settings.Hero.PrimaryButtonUrl,
                settings.Hero.SecondaryButtonText,
                settings.Hero.SecondaryButtonUrl,
                settings.Hero.BackgroundImageUrl,
                settings.Hero.OverlayImageUrl,
                settings.Hero.OverlayOpacity,
                settings.Hero.BadgeText,
                settings.Hero.BadgeColor,
                settings.Hero.Visible),
            new StorefrontAnnouncementBarDto(
                settings.AnnouncementBar.Text,
                settings.AnnouncementBar.Link,
                settings.AnnouncementBar.ButtonText,
                settings.AnnouncementBar.BackgroundColor,
                settings.AnnouncementBar.TextColor,
                settings.AnnouncementBar.Visible,
                settings.AnnouncementBar.DisplayPosition),
            new StorefrontPromoBannerDto(
                settings.PromotionalBanner.Title,
                settings.PromotionalBanner.Subtitle,
                settings.PromotionalBanner.ImageUrl,
                settings.PromotionalBanner.ButtonText,
                settings.PromotionalBanner.ButtonUrl,
                settings.PromotionalBanner.CountdownSeconds,
                settings.PromotionalBanner.CountdownEndsAtUtc,
                settings.PromotionalBanner.BackgroundColor,
                settings.PromotionalBanner.Visible),
            new StorefrontFlashDealsDto(
                settings.FlashDeals.SectionTitle,
                settings.FlashDeals.SectionSubtitle,
                settings.FlashDeals.CountdownEnabled,
                settings.FlashDeals.CountdownDateUtc,
                settings.FlashDeals.MaximumProducts,
                settings.FlashDeals.SortMethod,
                settings.FlashDeals.CountdownSeconds,
                settings.FlashDeals.Visible),
            new StorefrontFeaturedCollectionsDto(
                settings.FeaturedCollections.Title,
                settings.FeaturedCollections.Subtitle,
                settings.FeaturedCollections.MaximumItems,
                settings.FeaturedCollections.DisplayStyle,
                settings.FeaturedCollections.Visible),
            new StorefrontPopularCategoriesDto(
                settings.PopularCategories.SectionTitle,
                settings.PopularCategories.MaximumCategories,
                settings.PopularCategories.SortOrder,
                settings.PopularCategories.Visible),
            new StorefrontFeaturedProductDto(
                settings.FeaturedProduct.ProductId,
                settings.FeaturedProduct.Badge,
                settings.FeaturedProduct.CallToAction,
                settings.FeaturedProduct.Visible),
            settings.TrustBar
                .Where(t => t.Visible)
                .OrderBy(t => t.SortOrder)
                .Select(t => new StorefrontTrustItemDto(
                    t.Id, t.IconUrl, t.Title, t.Description, t.SortOrder, t.Visible))
                .ToList(),
            new StorefrontFooterDto(
                settings.Footer.CompanyName,
                settings.Footer.Copyright,
                settings.Footer.SupportEmail,
                settings.Footer.SupportPhone,
                settings.Footer.Address,
                settings.Footer.WorkingHours,
                settings.Footer.FacebookUrl,
                settings.Footer.InstagramUrl,
                settings.Footer.XUrl,
                settings.Footer.DiscordUrl,
                settings.Footer.TelegramUrl,
                settings.Footer.YouTubeUrl,
                settings.Footer.TikTokUrl),
            new StorefrontSeoDto(
                settings.Seo.DefaultMetaTitle,
                settings.Seo.DefaultMetaDescription,
                settings.Seo.OpenGraphImageUrl,
                settings.Seo.TwitterCard,
                settings.Seo.CanonicalUrl));

        return Result.Success(content);
    }
}
