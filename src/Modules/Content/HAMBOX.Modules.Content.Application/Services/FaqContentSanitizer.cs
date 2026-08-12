using Ganss.Xss;

namespace HAMBOX.Modules.Content.Application.Services;

/// <summary>
/// Server-side allow-list HTML sanitizer for FAQ answer rich text. This is the only real trust
/// boundary for this content: the frontend renders FAQ answers via
/// <c>DomSanitizer.bypassSecurityTrustHtml</c> on both <c>/faq</c> and the Page Builder FAQ
/// section — a deliberate opt-out of Angular's own sanitizer — so unsafe HTML must never be
/// allowed to reach storage in the first place.
///
/// Allows exactly the formatting the FAQ admin editor's toolbar can legitimately produce
/// (paragraphs, headings, bold/italic/underline/strikethrough, lists, links, blockquotes).
/// Strips everything else outright — scripts, iframes/embeds/objects, images, inline styles, and
/// any attribute or URL scheme capable of executing script (event handlers, <c>javascript:</c>/
/// <c>data:</c> links) — rather than encoding the whole answer as plain text, which would break
/// legitimate formatting.
/// </summary>
public static class FaqContentSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    /// <summary>Returns null for null input; otherwise the sanitized HTML.</summary>
    public static string? Sanitize(string? html)
    {
        return html is null ? null : Sanitizer.Sanitize(html);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer
        {
            // A stripped wrapper tag (e.g. a stray <div>/<span> from a paste) should not delete
            // the readable text/allowed formatting nested inside it. The one exception is tags
            // whose "text content" is actually code, not prose — handled below via RemovingTag,
            // since KeepChildNodes would otherwise unwrap e.g. <script>alert(1)</script> into the
            // inert-but-still-undesirable literal text "alert(1)" rather than removing it outright.
            KeepChildNodes = true,
        };

        // Tags whose content is code/data, not human-readable text — must be dropped entirely
        // (tag AND content), never unwrapped by KeepChildNodes.
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

        // No inline styles at all — color/background/etc. aren't part of the supported formatting
        // set, and dropping the style attribute entirely removes CSS-based XSS vectors outright
        // rather than trying to allow-list safe CSS properties.
        sanitizer.AllowedCssProperties.Clear();

        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto" })
        {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        return sanitizer;
    }
}
