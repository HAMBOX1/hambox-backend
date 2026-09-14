using System.Text;
using System.Text.RegularExpressions;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.MergeProducts;

/// <summary>
/// Mirrors the frontend's <c>buildDefaultSku</c> (product-variant-manager.component.ts) so
/// server-generated SKUs look like ones an admin would have typed by hand.
/// </summary>
internal static class SkuSlugifier
{
    public static string Slugify(string nameEn)
    {
        var upper = nameEn.Trim().ToUpperInvariant();
        var builder = new StringBuilder(upper.Length);
        foreach (var ch in upper)
        {
            builder.Append(char.IsAsciiLetterOrDigit(ch) ? ch : '-');
        }

        var collapsed = Regex.Replace(builder.ToString(), "-{2,}", "-").Trim('-');
        return collapsed.Length == 0 ? "DEFAULT" : collapsed;
    }
}
