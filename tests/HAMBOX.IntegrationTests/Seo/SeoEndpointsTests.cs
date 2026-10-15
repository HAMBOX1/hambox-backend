using HAMBOX.API.Extensions;

namespace HAMBOX.IntegrationTests.Seo;

/// <summary>Audit R54: robots.txt keeps private areas out of search results and points at the sitemap.</summary>
public sealed class SeoEndpointsTests
{
    [Fact]
    public void Robots_DisallowsPrivateAreas_AndAdvertisesTheSitemap()
    {
        var robots = SeoEndpoints.BuildRobots("index,follow", "https://store.example");

        Assert.Contains("User-agent: *", robots);
        Assert.Contains("Disallow: /admin/", robots);
        Assert.Contains("Disallow: /account/", robots);
        Assert.Contains("Disallow: /api/", robots);
        Assert.Contains("Sitemap: https://store.example/sitemap.xml", robots);
    }

    [Fact]
    public void Robots_WithNoindex_BlocksTheWholeSite()
    {
        var robots = SeoEndpoints.BuildRobots("noindex,nofollow", "https://store.example");

        Assert.Contains("Disallow: /", robots);
        Assert.DoesNotContain("Sitemap:", robots);
    }

    [Fact]
    public void Sitemap_ListsStaticPages_Products_AndCategories_WithEscapedUrls()
    {
        var xml = SeoEndpoints.BuildSitemap(
            "https://store.example",
            [
                new SeoEndpoints.SitemapEntry("/products/0f8fad5b-d9cb-469f-a165-70867728950e", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)),
                new SeoEndpoints.SitemapEntry("/categories/games&gift-cards", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)),
            ]);

        Assert.Contains("<loc>https://store.example/</loc>", xml);
        Assert.Contains("<loc>https://store.example/products</loc>", xml);
        Assert.Contains("<loc>https://store.example/products/0f8fad5b-d9cb-469f-a165-70867728950e</loc><lastmod>2026-10-01</lastmod>", xml);
        Assert.Contains("games&amp;gift-cards", xml);
        Assert.DoesNotContain("/admin", xml);
    }
}
