using HAMBOX.Infrastructure.Extensions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Xunit;

namespace HAMBOX.UnitTests.Infrastructure;

/// <summary>
/// Regression coverage for a MEDIUM security fix: the "HamboxCors" policy used to fall back to
/// <c>AllowAnyOrigin()</c> whenever <c>Cors:AllowedOrigins</c> was empty/missing — the wrong default
/// for a security-sensitive setting (fail open). <see cref="InfrastructureExtensions.ConfigureHamboxCorsPolicy"/>
/// is the exact policy-building logic <c>AddSharedInfrastructure</c> registers; these tests exercise
/// it directly, without booting the app, for both the empty-configuration and configured-origin cases.
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
}
