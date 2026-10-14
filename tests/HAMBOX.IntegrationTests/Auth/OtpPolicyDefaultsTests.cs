using HAMBOX.Modules.Identity.Application.Options;

namespace HAMBOX.IntegrationTests.Auth;

/// <summary>
/// Pins the shipped admin-OTP policy to the scope (§19.3–19.4): a code is valid for 5–10 minutes, allows
/// 3 attempts, and can be re-sent no more often than every 60 seconds.
/// </summary>
public sealed class OtpPolicyDefaultsTests
{
    private static readonly AdminOtpSettings Defaults = new();

    [Fact]
    public void Validity_IsBetweenFiveAndTenMinutes() =>
        Assert.InRange(Defaults.ExpirationMinutes, 5, 10);

    [Fact]
    public void MaxAttempts_IsThree() =>
        Assert.Equal(3, Defaults.MaxAttempts);

    [Fact]
    public void ResendCooldown_IsAtLeastSixtySeconds() =>
        Assert.True(Defaults.ResendCooldownSeconds >= 60);

    [Fact]
    public void CodeLength_IsSixDigits() =>
        Assert.Equal(6, Defaults.CodeLength);

    [Fact]
    public void LockoutAfterTooManyAttempts_IsNotZero() =>
        Assert.True(Defaults.LockoutMinutes > 0);
}
