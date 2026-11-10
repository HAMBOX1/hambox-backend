using HAMBOX.Application.Security;
using Xunit;

namespace HAMBOX.UnitTests.Infrastructure;

public sealed class RichTextSanitizerTests
{
    [Theory]
    [InlineData("<p>Hi</p><script>alert(1)</script>", "alert")]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<p onclick=\"x()\">Hi</p>", "onclick")]
    [InlineData("<a href=\"javascript:alert(1)\">l</a>", "javascript:")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>", "iframe")]
    [InlineData("<p style=\"background:url(javascript:x)\">Hi</p>", "url(")]
    [InlineData("<p style=\"position:fixed;top:0\">Hi</p>", "position")]
    [InlineData("<style>body{display:none}</style><p>Hi</p>", "display")]
    public void Sanitize_RemovesActiveContent(string input, string mustNotContain) =>
        Assert.DoesNotContain(mustNotContain, RichTextSanitizer.Sanitize(input), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Sanitize_KeepsNormalFormatting()
    {
        const string html = "<h2>Steps</h2><ul><li><strong>One</strong></li></ul><p><a href=\"https://example.com\">link</a></p><img src=\"/uploads/a.png\" alt=\"a\">";

        var result = RichTextSanitizer.Sanitize(html);

        Assert.Contains("<h2>Steps</h2>", result);
        Assert.Contains("<strong>One</strong>", result);
        Assert.Contains("href=\"https://example.com\"", result);
        Assert.Contains("src=\"/uploads/a.png\"", result);
    }

    [Fact]
    public void Sanitize_KeepsEditorColourAndAlignmentStyles()
    {
        var result = RichTextSanitizer.Sanitize("<p class=\"ql-align-center\" style=\"color: rgb(230, 0, 0); text-align: center\">Hi</p>");

        Assert.Contains("color", result);
        Assert.Contains("text-align", result);
        Assert.Contains("ql-align-center", result);
    }

    [Fact]
    public void Sanitize_Null_ReturnsNull() => Assert.Null(RichTextSanitizer.Sanitize(null));
}
