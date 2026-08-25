using HAMBOX.Application.Security;
using HAMBOX.Infrastructure.Security;

namespace HAMBOX.UnitTests.Infrastructure;

/// <summary>
/// Mirrors <c>JwtSettingsValidator</c>'s own test coverage expectations: an unconfigured SecretKey must
/// fail application startup (<c>ValidateOnStart</c> in <c>InfrastructureExtensions</c>), never silently
/// fall back to an unverified/always-open state.
/// </summary>
public sealed class TurnstileSettingsValidatorTests
{
    private static readonly TurnstileSettingsValidator Validator = new();

    [Fact]
    public void MissingSecretKey_FailsValidation()
    {
        var result = Validator.Validate(null, new TurnstileSettings { SiteKey = "site-key", SecretKey = "" });

        Assert.True(result.Failed);
    }

    [Fact]
    public void MissingSiteKey_FailsValidation()
    {
        var result = Validator.Validate(null, new TurnstileSettings { SiteKey = "", SecretKey = "secret-key" });

        Assert.True(result.Failed);
    }

    [Fact]
    public void ValidSettings_PassesValidation()
    {
        var result = Validator.Validate(null, new TurnstileSettings { SiteKey = "site-key", SecretKey = "secret-key" });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public void InvalidRequestTimeout_FailsValidation(int timeoutSeconds)
    {
        var result = Validator.Validate(
            null, new TurnstileSettings { SiteKey = "site-key", SecretKey = "secret-key", RequestTimeoutSeconds = timeoutSeconds });

        Assert.True(result.Failed);
    }
}
