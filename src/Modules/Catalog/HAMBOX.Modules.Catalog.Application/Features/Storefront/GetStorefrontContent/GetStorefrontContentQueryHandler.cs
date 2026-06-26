using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Options;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Catalog.Application.Features.Storefront.GetStorefrontContent;

internal sealed class GetStorefrontContentQueryHandler(
    IOptions<StorefrontSettings> storefrontSettings)
    : IRequestHandler<GetStorefrontContentQuery, Result<StorefrontContentDto>>
{
    public Task<Result<StorefrontContentDto>> Handle(
        GetStorefrontContentQuery request,
        CancellationToken cancellationToken)
    {
        var settings = storefrontSettings.Value;

        var content = new StorefrontContentDto(
            new StorefrontHeroDto(
                settings.Hero.Eyebrow,
                settings.Hero.TitleLine1,
                settings.Hero.TitleAccent,
                settings.Hero.Description,
                settings.Hero.BackgroundImageUrl,
                settings.Hero.OverlayImageUrl,
                settings.Hero.PrimaryCtaLabel,
                settings.Hero.PrimaryCtaRoute,
                settings.Hero.SecondaryCtaLabel,
                settings.Hero.SecondaryCtaRoute),
            new StorefrontPromoBannerDto(
                settings.PromoBanner.Headline,
                settings.PromoBanner.Subheadline,
                settings.PromoBanner.BackgroundImageUrl,
                settings.PromoBanner.CountdownSeconds),
            settings.FlashDealsCountdownSeconds);

        return Task.FromResult(Result.Success(content));
    }
}
