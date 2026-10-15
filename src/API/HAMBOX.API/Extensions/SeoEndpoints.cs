using System.Security;
using HAMBOX.Application.Abstractions;
using System.Text;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HAMBOX.API.Extensions;

/// <summary>
/// <c>/sitemap.xml</c> and <c>/robots.txt</c> for search engines (audit R54). The storefront is a SPA, so the
/// sitemap is what lets crawlers find product and category URLs without executing the app. Both are served from
/// the API (nginx forwards them) so they always reflect the live catalogue and the SEO platform settings.
/// </summary>
internal static class SeoEndpoints
{
    private const string SitemapCacheKey = "seo:sitemap";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    /// <summary>Paths that must never be indexed: back office, account area, checkout and the API itself.</summary>
    internal static readonly string[] DisallowedPaths =
        ["/admin/", "/account/", "/cart", "/checkout", "/auth/", "/api/", "/uploads/private/"];

    private static readonly string[] StaticPaths = ["/", "/products", "/faq", "/legal"];

    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", async (HttpContext http, IPlatformSettingsProvider settings, CancellationToken ct) =>
        {
            var seo = await settings.GetAsync<SeoSettingsPayload>(PlatformSettingsCategoryKeys.Seo, ct);
            var baseUrl = ResolveBaseUrl(http, seo);
            return Results.Text(BuildRobots(seo.RobotsDirective, baseUrl), "text/plain; charset=utf-8");
        })
        .WithName("GetRobotsTxt")
        .AllowAnonymous();

        app.MapGet("/sitemap.xml", async (
            HttpContext http,
            IPlatformSettingsProvider settings,
            ICatalogDbContext catalog,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            var seo = await settings.GetAsync<SeoSettingsPayload>(PlatformSettingsCategoryKeys.Seo, ct);
            var baseUrl = ResolveBaseUrl(http, seo);
            var key = SitemapCacheKey + ":" + baseUrl;

            if (!cache.TryGetValue(key, out string? xml) || xml is null)
            {
                var products = await catalog.Products
                    .AsNoTracking()
                    .Where(p => p.Status == ProductStatus.Active)
                    .Select(p => new SitemapEntry("/products/" + p.Id, p.ModifiedOnUtc ?? p.CreatedOnUtc))
                    .ToListAsync(ct);

                var categories = await catalog.Categories
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .Select(c => new SitemapEntry("/categories/" + c.Slug, c.ModifiedOnUtc ?? c.CreatedOnUtc))
                    .ToListAsync(ct);

                xml = BuildSitemap(baseUrl, products.Concat(categories));
                cache.Set(key, xml, CacheDuration);
            }

            return Results.Text(xml, "application/xml; charset=utf-8");
        })
        .WithName("GetSitemapXml")
        .AllowAnonymous();

        return app;
    }

    internal static string ResolveBaseUrl(HttpContext http, SeoSettingsPayload seo)
    {
        if (Uri.TryCreate(seo.CanonicalBaseUrl, UriKind.Absolute, out var configured)
            && !configured.IsLoopback
            && (configured.Scheme == Uri.UriSchemeHttps || configured.Scheme == Uri.UriSchemeHttp))
        {
            return configured.GetLeftPart(UriPartial.Authority);
        }

        return $"{http.Request.Scheme}://{http.Request.Host}";
    }

    internal static string BuildRobots(string? robotsDirective, string baseUrl)
    {
        var builder = new StringBuilder();
        builder.AppendLine("User-agent: *");

        if (robotsDirective?.Contains("noindex", StringComparison.OrdinalIgnoreCase) == true)
        {
            // The store owner asked for the whole site to stay out of search results.
            builder.AppendLine("Disallow: /");
            return builder.ToString();
        }

        foreach (var path in DisallowedPaths)
        {
            builder.Append("Disallow: ").AppendLine(path);
        }

        builder.AppendLine();
        builder.Append("Sitemap: ").Append(baseUrl).AppendLine("/sitemap.xml");
        return builder.ToString();
    }

    internal static string BuildSitemap(string baseUrl, IEnumerable<SitemapEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var path in StaticPaths)
        {
            builder.Append("  <url><loc>").Append(SecurityElement.Escape(baseUrl + path)).AppendLine("</loc></url>");
        }

        foreach (var entry in entries)
        {
            builder.Append("  <url><loc>").Append(SecurityElement.Escape(baseUrl + entry.Path)).Append("</loc>")
                .Append("<lastmod>").Append(entry.LastModified.UtcDateTime.ToString("yyyy-MM-dd")).AppendLine("</lastmod></url>");
        }

        builder.AppendLine("</urlset>");
        return builder.ToString();
    }

    internal sealed record SitemapEntry(string Path, DateTimeOffset LastModified);
}
