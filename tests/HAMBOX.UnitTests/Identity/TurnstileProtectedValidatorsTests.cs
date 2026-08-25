using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Application.Referrals;
using HAMBOX.Application.Security;
using HAMBOX.SharedKernel.Results;
using HAMBOX.Modules.Identity.Application.Features.ForgotPassword;
using HAMBOX.Modules.Identity.Application.Features.Register;
using HAMBOX.Modules.Identity.Application.Features.ResendVerification;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Proves the three account-action commands protected by Cloudflare Turnstile
/// (<see cref="RegisterCommand"/>, <see cref="ForgotPasswordCommand"/>, <see cref="ResendVerificationCommand"/>)
/// cannot pass validation — and therefore cannot reach their handler, since <c>ValidationBehavior</c> sits
/// in front of every handler — without a token <see cref="ITurnstileVerificationService"/> accepts. A
/// caller that omits the widget entirely (empty token) is rejected exactly the same way as one that
/// supplies a token Cloudflare rejects.
/// </summary>
public sealed class TurnstileProtectedValidatorsTests
{
    private sealed class RecordingTurnstileService(bool accept) : ITurnstileVerificationService
    {
        public string? LastToken { get; private set; }
        public string? LastRemoteIp { get; private set; }
        public string? LastExpectedAction { get; private set; }
        public int CallCount { get; private set; }

        public Task<bool> VerifyAsync(string? token, string? remoteIp, string? expectedAction, CancellationToken cancellationToken)
        {
            CallCount++;
            LastToken = token;
            LastRemoteIp = remoteIp;
            LastExpectedAction = expectedAction;
            return Task.FromResult(accept);
        }
    }

    private sealed class FakePlatformSettingsProvider : IPlatformSettingsProvider
    {
        private static NotSupportedException NotNeeded() => new("Not needed by this test.");

        public Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthenticationSettingsPayload(8, false, false, false, 60, 30, false));

        public Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<OtpSettingsPayload> GetOtpAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<InventorySettingsPayload> GetInventoryAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<CurrencySettingsPayload> GetCurrencyAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<MediaSettingsPayload> GetMediaAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<MaintenanceSettingsPayload> GetMaintenanceAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<bool> VerifyMaintenanceBypassPasswordAsync(string password, CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<StorefrontContentSettingsPayload> GetStorefrontAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public Task<PublicPlatformSettingsDto> GetPublicSettingsAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
        public void InvalidateCache(string? categoryKey = null) => throw NotNeeded();
    }

    private sealed class NeverInvokedReferralRedemptionService : IReferralRedemptionService
    {
        public Task<bool> ReferralCodeExistsAsync(string referralCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No referral code is supplied by these tests.");

        public Task<Result> RedeemAsync(string referralCode, string referredUserId, string referredEmail, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Registration validation never redeems a code.");
    }

    private static RegisterCommand ValidRegisterCommand(string turnstileToken) =>
        new("user@example.com", "Password1!", "First", "Last", "203.0.113.1", "test-agent", "en", null, turnstileToken);

    [Fact]
    public async Task Register_ValidToken_PassesValidation()
    {
        var turnstile = new RecordingTurnstileService(accept: true);
        var validator = new RegisterCommandValidator(new FakePlatformSettingsProvider(), new NeverInvokedReferralRedemptionService(), turnstile);

        var result = await validator.ValidateAsync(ValidRegisterCommand("good-token"));

        Assert.True(result.IsValid);
        Assert.Equal("register", turnstile.LastExpectedAction);
        Assert.Equal("203.0.113.1", turnstile.LastRemoteIp);
    }

    [Fact]
    public async Task Register_RejectedToken_FailsValidation()
    {
        var turnstile = new RecordingTurnstileService(accept: false);
        var validator = new RegisterCommandValidator(new FakePlatformSettingsProvider(), new NeverInvokedReferralRedemptionService(), turnstile);

        var result = await validator.ValidateAsync(ValidRegisterCommand("bad-token"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCommand.TurnstileToken));
    }

    [Fact]
    public async Task Register_MissingToken_FailsValidation_WithoutRevealingWhy()
    {
        var turnstile = new RecordingTurnstileService(accept: false);
        var validator = new RegisterCommandValidator(new FakePlatformSettingsProvider(), new NeverInvokedReferralRedemptionService(), turnstile);

        var result = await validator.ValidateAsync(ValidRegisterCommand(string.Empty));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, e => e.PropertyName == nameof(RegisterCommand.TurnstileToken));
        Assert.Equal("Security verification failed. Please try again.", error.ErrorMessage);
    }

    [Fact]
    public async Task ForgotPassword_ValidToken_PassesValidation()
    {
        var turnstile = new RecordingTurnstileService(accept: true);
        var validator = new ForgotPasswordCommandValidator(turnstile);

        var result = await validator.ValidateAsync(new ForgotPasswordCommand("user@example.com", "203.0.113.1", "good-token"));

        Assert.True(result.IsValid);
        Assert.Equal("forgot-password", turnstile.LastExpectedAction);
    }

    [Fact]
    public async Task ForgotPassword_InvalidToken_FailsValidation()
    {
        var turnstile = new RecordingTurnstileService(accept: false);
        var validator = new ForgotPasswordCommandValidator(turnstile);

        var result = await validator.ValidateAsync(new ForgotPasswordCommand("user@example.com", "203.0.113.1", "bad-token"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ResendVerification_ValidToken_PassesValidation()
    {
        var turnstile = new RecordingTurnstileService(accept: true);
        var validator = new ResendVerificationCommandValidator(turnstile);

        var result = await validator.ValidateAsync(new ResendVerificationCommand("user@example.com", "203.0.113.1", "good-token"));

        Assert.True(result.IsValid);
        Assert.Equal("resend-verification", turnstile.LastExpectedAction);
    }

    [Fact]
    public async Task ResendVerification_InvalidToken_FailsValidation()
    {
        var turnstile = new RecordingTurnstileService(accept: false);
        var validator = new ResendVerificationCommandValidator(turnstile);

        var result = await validator.ValidateAsync(new ResendVerificationCommand("user@example.com", "203.0.113.1", "bad-token"));

        Assert.False(result.IsValid);
    }
}
