using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Features;
using HAMBOX.Modules.Identity.Application.Features.ChangePassword;
using HAMBOX.Modules.Identity.Application.Features.ResetPassword;
using Xunit;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Change-password and reset-password used to hard-code an 8-character minimum and ignore the configured
/// policy (Settings > Authentication); only registration honoured it.
/// </summary>
public sealed class PasswordPolicyRulesTests
{
    private static AuthenticationSettingsPayload Policy(int min, bool numbers = false, bool upper = false, bool symbols = false) =>
        new(min, numbers, symbols, upper, 15, 30, true);

    [Theory]
    [InlineData(14, false, false, false, "Short#Pass1", false)]
    [InlineData(14, false, false, false, "A-Long-Enough-Pass-1", true)]
    [InlineData(8, true, false, false, "nodigitshere", false)]
    [InlineData(8, true, false, false, "has1digit", true)]
    [InlineData(8, false, true, false, "alllowercase1", false)]
    [InlineData(8, false, false, true, "NoSymbols123", false)]
    [InlineData(6, false, false, false, "sixchr", true)]
    public async Task ChangePassword_EnforcesTheConfiguredPolicy(int min, bool numbers, bool upper, bool symbols, string newPassword, bool expectedValid)
    {
        var validator = new ChangePasswordCommandValidator(new Provider(Policy(min, numbers, upper, symbols)));

        var result = await validator.ValidateAsync(new ChangePasswordCommand("current", newPassword));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task ResetPassword_EnforcesTheConfiguredPolicy()
    {
        var validator = new ResetPasswordCommandValidator(new Provider(Policy(14)));

        Assert.False((await validator.ValidateAsync(new ResetPasswordCommand("token", "Short#Pass1"))).IsValid);
        Assert.True((await validator.ValidateAsync(new ResetPasswordCommand("token", "A-Long-Enough-Pass-1"))).IsValid);
    }

    [Fact]
    public void SharedRule_IsPubliclyAvailableForOtherValidators() =>
        Assert.NotNull(typeof(PasswordPolicyRules).GetMethod(nameof(PasswordPolicyRules.MeetsConfiguredPasswordPolicy)));

    private sealed class Provider(AuthenticationSettingsPayload authentication) : IPlatformSettingsProvider
    {
        private static NotSupportedException NotNeeded() => new("Not needed by these tests.");

        public Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(authentication);

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
}
