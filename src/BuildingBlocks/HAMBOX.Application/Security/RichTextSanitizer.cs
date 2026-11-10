using Ganss.Xss;

namespace HAMBOX.Application.Security;

/// <summary>
/// Server-side allow-list sanitizer for admin-authored rich text that the storefront renders as HTML
/// (legal documents, knowledge-base articles, product instructions). Several storefront pages opt out of
/// Angular's own sanitizer for this content, so this is the real trust boundary: unsafe HTML must never reach
/// storage. Keeps normal formatting (headings, lists, links, images, tables, editor alignment classes) and
/// removes scripts, stylesheets, frames, event handlers, unsafe inline styles and <c>javascript:</c> / <c>data:</c> URLs.
/// </summary>
public static class RichTextSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = Create();

    /// <summary>Returns <c>null</c> for <c>null</c> input; otherwise the sanitized HTML.</summary>
    public static string? Sanitize(string? html) => html is null ? null : Sanitizer.Sanitize(html);

    private static HtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer
        {
            // A stripped wrapper (stray div / span from a paste) keeps the text inside it; code-bearing tags
            // are emptied below so their contents are not unwrapped into visible text.
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
                     "p", "br", "hr", "span", "div", "strong", "b", "em", "i", "u", "s", "sub", "sup",
                     "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "a", "blockquote", "pre", "code",
                     "img", "table", "thead", "tbody", "tr", "th", "td",
                 })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[] { "href", "src", "alt", "title", "class", "style", "target", "rel", "colspan", "rowspan" })
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        // The editor toolbar offers text colour, alignment and font choices, which it stores as inline styles.
        // Only these presentational properties survive; everything else (background images, position,
        // behaviour / url() values) is dropped.
        sanitizer.AllowedCssProperties.Clear();
        foreach (var property in new[]
                 {
                     "color", "background-color", "text-align", "text-decoration", "font-weight",
                     "font-style", "font-size", "font-family", "direction",
                 })
        {
            sanitizer.AllowedCssProperties.Add(property);
        }

        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto" })
        {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        return sanitizer;
    }
}
