using System.Text.RegularExpressions;
using Ganss.Xss;

namespace HAMBOX.Modules.Catalog.Application.Services;

/// <summary>
/// Server-side allow-list HTML sanitizer for optional product-option-value instructions (e.g.
/// "Requires an Argentina-region account..."). This is the real trust boundary for this content:
/// the frontend renders it via a plain <c>[innerHTML]</c> binding with no
/// <c>DomSanitizer.bypassSecurityTrustHtml</c> call, so unsafe HTML must never be allowed to reach
/// storage in the first place. Same allow-list/approach as
/// <c>HAMBOX.Modules.Content.Application.Services.FaqContentSanitizer</c> — duplicated rather than
/// shared across modules, since Catalog has no existing reference to Content and this is a single
/// small static class.
/// </summary>
public static class ProductOptionDescriptionSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    /// <summary>Returns null for null input, and null (not empty string) when the input sanitizes
    /// down to nothing — a sanitized-empty description is treated as no description at all.</summary>
    public static string? Sanitize(string? html)
    {
        if (html is null)
        {
            return null;
        }

        var sanitized = Sanitizer.Sanitize(html);

        // An empty allowed tag (e.g. "<p></p>" left behind after stripping disallowed content)
        // carries no visible text — treat it the same as an empty string, not as "has a description".
        var textOnly = TagPattern.Replace(sanitized, string.Empty);
        return string.IsNullOrWhiteSpace(textOnly) ? null : sanitized;
    }

    private static readonly Regex TagPattern = new("<[^>]*>", RegexOptions.Compiled);

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer
        {
            KeepChildNodes = true,
        };

        sanitizer.RemovingTag += (_, args) =>
        {
            if (args.Tag.NodeName is "SCRIPT" or "STYLE")
            {
                args.Tag.TextContent = string.Empty;
            }
        };

        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
                 {
                     "p", "br", "strong", "b", "em", "i", "u", "s",
                     "h1", "h2", "h3", "h4", "h5", "h6",
                     "ul", "ol", "li", "a", "blockquote",
                 })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");

        sanitizer.AllowedCssProperties.Clear();

        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto" })
        {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        return sanitizer;
    }
}
