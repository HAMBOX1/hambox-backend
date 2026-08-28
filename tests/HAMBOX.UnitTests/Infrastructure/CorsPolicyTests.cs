using HAMBOX.Infrastructure.Extensions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Xunit;

namespace HAMBOX.UnitTests.Infrastructure;

/// <summary>
/// Regression coverage for a security fix: the "HamboxCors" policy used to fall back to
/// <c>AllowAnyOrigin()</c> whenever <c>Cors:AllowedOrigins</c> was empty, missing, or malformed — the
/// wrong default for a security-sensitive setting (fail open). <see cref="InfrastructureExtensions.ConfigureHamboxCorsPolicy"/>
/// is the exact policy-building logic <c>AddSharedInfrastructure</c> registers; these tests exercise
/// it directly, without booting the app, across the empty/unconfigured, malformed, single-origin, and
/// multi-origin cases.
/// </summary>
public sealed class CorsPolicyTests
{
    [Fact]
    public void ConfigureHamboxCorsPolicy_EmptyAllowedOrigins_NeverAllowsAnyOrigin()
    {
        var builder = new CorsPolicyBuilder();
        InfrastructureExtensions.ConfigureHamboxCorsPolicy(builder, []);
        var policy = builder.Build();

        Assert.False(policy.AllowAnyOrigin);
        Assert.Empty(policy.Origins);
        Assert.False(policy.IsOriginAllowed("https://evil.example.com"));
        Assert.False(policy.IsOriginAllowed("http://localhost:4200"));
    }

    [Fact]
    public void ConfigureHamboxCorsPolicy_ConfiguredOrigins_AllowsOnlyThoseExactOrigins()
    {
        var builder = new CorsPolicyBuilder();
        InfrastructureExtensions.ConfigureHamboxCorsPolicy(builder, ["https://app.hambox.example"]);
        var policy = builder.Build();

        Assert.False(policy.AllowAnyOrigin);
        Assert.True(policy.IsOriginAllowed("https://app.hambox.example"));
        Assert.False(policy.IsOriginAllowed("https://evil.example.com"));
        Assert.False(policy.IsOriginAllowed("http://app.hambox.example")); // scheme must match too
    }

    [Fact]
    public void ConfigureHamboxCorsPolicy_UnconfiguredOrigin_IsNeverAllowed()
    {
        var builder = new CorsPolicyBuilder();
        InfrastructureExtensions.ConfigureHamboxCorsPolicy(builder, ["https://app.hambox.example"]);
        var policy = builder.Build();

        Assert.False(policy.IsOriginAllowed("https://not-in-the-list.example.com"));
    }

    [Fact]
    public void ConfigureHamboxCorsPolicy_WhitespaceOnlyEntries_FailsClosed()
    {
        var builder = new CorsPolicyBuilder();
        InfrastructureExtensions.ConfigureHamboxCorsPolicy(builder, ["", "   ", null!]);
        var policy = builder.Build();

        Assert.False(policy.AllowAnyOrigin);
        Assert.Empty(policy.Origins);
        Assert.False(policy.IsOriginAllowed("https://evil.example.com"));
    }

    [Fact]
    public void ConfigureHamboxCorsPolicy_MalformedEntries_FailsClosed()
    {
        var builder = new CorsPolicyBuilder();
        // Not absolute URIs — a relative path and plain garbage text, the shape a fat-fingered
        // config value would actually take.
        InfrastructureExtensions.ConfigureHamboxCorsPolicy(builder, ["/relative/path", "not a valid origin"]);
        var policy = builder.Build();

        Assert.False(policy.AllowAnyOrigin);
        Assert.Empty(policy.Origins);
        Assert.False(policy.IsOriginAllowed("https://evil.example.com"));
        Assert.False(policy.IsOriginAllowed("not a valid origin"));
    }

    [Fact]
    public void ConfigureHamboxCorsPolicy_MixOfValidAndMalformedEntries_OnlyValidOnesAreUsed()
    {
        var builder = new CorsPolicyBuilder();
        InfrastructureExtensions.ConfigureHamboxCorsPolicy(
            builder, ["https://app.hambox.example", "not a valid origin", ""]);
        var policy = builder.Build();

        Assert.False(policy.AllowAnyOrigin);
        Assert.True(policy.IsOriginAllowed("https://app.hambox.example"));
        Assert.False(policy.IsOriginAllowed("https://evil.example.com"));
    }

    [Fact]
    public void ConfigureHamboxCorsPolicy_MultipleConfiguredOrigins_AllowsOnlyThoseOrigins()
    {
        var builder = new CorsPolicyBuilder();
        InfrastructureExtensions.ConfigureHamboxCorsPolicy(
            builder, ["https://app.hambox.example", "https://admin.hambox.example"]);
        var policy = builder.Build();

        Assert.False(policy.AllowAnyOrigin);
        Assert.True(policy.IsOriginAllowed("https://app.hambox.example"));
        Assert.True(policy.IsOriginAllowed("https://admin.hambox.example"));
        Assert.False(policy.IsOriginAllowed("https://evil.example.com"));
        Assert.Equal(2, policy.Origins.Count);
    }

    [Fact]
    public void ConfigureHamboxCorsPolicy_ConfiguredOrigins_AllowsCredentials()
    {
        // The frontend sends the auth cookie/bearer token cross-origin; a configured-origins policy
        // must still permit credentials, or legitimate requests break.
        var builder = new CorsPolicyBuilder();
        InfrastructureExtensions.ConfigureHamboxCorsPolicy(builder, ["https://app.hambox.example"]);
        var policy = builder.Build();

        Assert.True(policy.SupportsCredentials);
    }
}
