using HAMBOX.Modules.Identity.Presentation.Authentication;
using Microsoft.AspNetCore.Http;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Proves <see cref="CsrfCookieWriter.ValidateCsrfToken"/>'s double-submit check in isolation, without
/// booting the app — mirrors <c>AuthCookieWriterTests</c>' direct-unit-test-of-a-pure-function
/// precedent. The refresh/logout endpoints call this before touching the refresh cookie at all, so
/// its failure modes (missing cookie, missing header, mismatched values) must fail closed.
/// </summary>
public sealed class CsrfCookieWriterTests
{
    private static HttpContext BuildContext(string? cookieValue, string? headerValue)
    {
        var context = new DefaultHttpContext();

        if (cookieValue is not null)
        {
            context.Request.Headers.Append("Cookie", $"{CsrfCookieWriter.CookieName}={cookieValue}");
        }

        if (headerValue is not null)
        {
            context.Request.Headers[CsrfCookieWriter.HeaderName] = headerValue;
        }

        return context;
    }

    [Fact]
    public void ValidateCsrfToken_MatchingCookieAndHeader_ReturnsTrue()
    {
        var context = BuildContext("same-token-value", "same-token-value");

        Assert.True(CsrfCookieWriter.ValidateCsrfToken(context));
    }

    [Fact]
    public void ValidateCsrfToken_MismatchedCookieAndHeader_ReturnsFalse()
    {
        var context = BuildContext("cookie-value", "different-header-value");

        Assert.False(CsrfCookieWriter.ValidateCsrfToken(context));
    }

    [Fact]
    public void ValidateCsrfToken_MissingCookie_ReturnsFalse()
    {
        var context = BuildContext(cookieValue: null, headerValue: "some-header-value");

        Assert.False(CsrfCookieWriter.ValidateCsrfToken(context));
    }

    [Fact]
    public void ValidateCsrfToken_MissingHeader_ReturnsFalse()
    {
        var context = BuildContext(cookieValue: "some-cookie-value", headerValue: null);

        Assert.False(CsrfCookieWriter.ValidateCsrfToken(context));
    }

    [Fact]
    public void ValidateCsrfToken_BothMissing_ReturnsFalse()
    {
        var context = BuildContext(cookieValue: null, headerValue: null);

        Assert.False(CsrfCookieWriter.ValidateCsrfToken(context));
    }

    [Fact]
    public void ValidateCsrfToken_EmptyCookie_ReturnsFalse()
    {
        var context = BuildContext(cookieValue: "", headerValue: "some-header-value");

        Assert.False(CsrfCookieWriter.ValidateCsrfToken(context));
    }

    [Fact]
    public void ValidateCsrfToken_DifferentLengthValues_ReturnsFalse()
    {
        var context = BuildContext(cookieValue: "short", headerValue: "a-much-longer-value-entirely");

        Assert.False(CsrfCookieWriter.ValidateCsrfToken(context));
    }
}
