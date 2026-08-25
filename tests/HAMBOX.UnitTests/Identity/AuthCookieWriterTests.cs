using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Presentation.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Proves the refresh-token cookie's security attributes without booting the app — mirrors
/// <c>CorsPolicyTests</c>' direct-unit-test-of-an-extracted-pure-function precedent
/// (<c>InfrastructureExtensions.ConfigureHamboxCorsPolicy</c>). Always HttpOnly; Secure follows the
/// same <c>!IsDevelopment()</c> gate <c>Program.cs</c> uses for HSTS/HTTPS redirection; SameSite/Path
/// come from configuration.
/// </summary>
public sealed class AuthCookieWriterTests
{
    private static readonly RefreshCookieSettings DefaultSettings = new();

    [Fact]
    public void BuildCookieOptions_AlwaysHttpOnly()
    {
        var options = AuthCookieWriter.BuildCookieOptions(
            DefaultSettings, new FakeHostEnvironment(Environments.Production), DateTimeOffset.UtcNow);

        Assert.True(options.HttpOnly);
    }

    [Fact]
    public void BuildCookieOptions_Development_IsNotSecure()
    {
        var options = AuthCookieWriter.BuildCookieOptions(
            DefaultSettings, new FakeHostEnvironment(Environments.Development), DateTimeOffset.UtcNow);

        // A Secure cookie set over plain http://localhost (how local dev actually runs) would never
        // reach the browser at all — this must be false in Development, not just "safer if true".
        Assert.False(options.Secure);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    [InlineData("Staging")]
    public void BuildCookieOptions_NonDevelopment_IsSecure(string environmentName)
    {
        var options = AuthCookieWriter.BuildCookieOptions(
            DefaultSettings, new FakeHostEnvironment(environmentName), DateTimeOffset.UtcNow);

        Assert.True(options.Secure);
    }

    [Fact]
    public void BuildCookieOptions_UsesConfiguredPathAndSameSite()
    {
        var settings = new RefreshCookieSettings { Path = "/api/auth", SameSite = "Strict" };

        var options = AuthCookieWriter.BuildCookieOptions(
            settings, new FakeHostEnvironment(Environments.Production), DateTimeOffset.UtcNow);

        Assert.Equal("/api/auth", options.Path);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
    }

    [Theory]
    [InlineData("None", SameSiteMode.None)]
    [InlineData("none", SameSiteMode.None)]
    [InlineData("Lax", SameSiteMode.Lax)]
    [InlineData("garbage-unrecognized-value", SameSiteMode.Lax)] // fail closed to the safer default
    public void BuildCookieOptions_ParsesSameSite_CaseInsensitive_DefaultsToLax(string configured, SameSiteMode expected)
    {
        var settings = new RefreshCookieSettings { SameSite = configured };

        var options = AuthCookieWriter.BuildCookieOptions(
            settings, new FakeHostEnvironment(Environments.Production), DateTimeOffset.UtcNow);

        Assert.Equal(expected, options.SameSite);
    }

    [Fact]
    public void BuildCookieOptions_NoDomainConfigured_LeavesDomainNull()
    {
        var options = AuthCookieWriter.BuildCookieOptions(
            DefaultSettings, new FakeHostEnvironment(Environments.Production), DateTimeOffset.UtcNow);

        // Scoped to whichever host the browser actually contacted — correct for the documented
        // reverse-proxy topology where the browser never sees the real API origin.
        Assert.Null(options.Domain);
    }

    [Fact]
    public void BuildCookieOptions_SetsExpiryToTheSuppliedValue_NotAFixedDefault()
    {
        var refreshExpiresOnUtc = DateTimeOffset.UtcNow.AddDays(90); // e.g. a remember-me duration

        var options = AuthCookieWriter.BuildCookieOptions(
            DefaultSettings, new FakeHostEnvironment(Environments.Production), refreshExpiresOnUtc);

        Assert.Equal(refreshExpiresOnUtc, options.Expires);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HAMBOX.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
