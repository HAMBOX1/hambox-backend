using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>
/// A configurable <see cref="IPlatformSettingsProvider"/> stand-in for tests exercising
/// <c>SupplierPricingEngine</c>, which reads only the <c>commerce</c> category's
/// <see cref="CommerceSettingsPayload.DefaultSupplierMarginPercent"/>. Every other member throws, same
/// "fail loudly on an unexpected call" discipline as <see cref="FakePlatformSettingsProvider"/>.
/// </summary>
internal sealed class FakeCommerceSettingsProvider : IPlatformSettingsProvider
{
    public CommerceSettingsPayload Commerce { get; set; } = new(0m, false, 15, 24, 14, "ORD-", DefaultSupplierMarginPercent: 20m);

    /// <summary>Disabled by default — checkout flows that complete an order exercise <c>ReferralLifecycleService</c>
    /// as a side effect, and this fake exists to let that no-op cleanly rather than throw, for tests
    /// that are exclusively about supplier pricing.</summary>
    public ReferralSettingsPayload Referral { get; set; } = new(Enabled: false, PointsPerReferral: 0, PointValueUsd: 0m, RewardExpiryDays: 0);

    public RetryPoliciesSettingsPayload RetryPolicies { get; set; } = new(3, 30, 120, 5, UseExponentialBackoff: false);

    private static NotSupportedException NotNeeded() => new("Not needed by these tests.");

    public Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default)
    {
        if (categoryKey == PlatformSettingsCategoryKeys.Commerce && Commerce is T commerce)
        {
            return Task.FromResult(commerce);
        }

        if (categoryKey == PlatformSettingsCategoryKeys.Referral && Referral is T referral)
        {
            return Task.FromResult(referral);
        }

        if (categoryKey == PlatformSettingsCategoryKeys.RetryPolicies && RetryPolicies is T retryPolicies)
        {
            return Task.FromResult(retryPolicies);
        }

        throw NotNeeded();
    }

    public Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<OtpSettingsPayload> GetOtpAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public InventorySettingsPayload Inventory { get; set; } = new(5, 15, true, "Immediate");

    public Task<InventorySettingsPayload> GetInventoryAsync(CancellationToken cancellationToken = default) => Task.FromResult(Inventory);
    public CurrencySettingsPayload Currency { get; set; } = new(
        "USD", ["USD", "EUR", "EGP"], 60, "Static", null,
        new Dictionary<string, decimal> { ["USD"] = 1m, ["EUR"] = 0.92m, ["EGP"] = 48.5m });

    public Task<CurrencySettingsPayload> GetCurrencyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Currency);
    public Task<MediaSettingsPayload> GetMediaAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<MaintenanceSettingsPayload> GetMaintenanceAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<bool> VerifyMaintenanceBypassPasswordAsync(string password, CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<StorefrontContentSettingsPayload> GetStorefrontAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<PublicPlatformSettingsDto> GetPublicSettingsAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public void InvalidateCache(string? categoryKey = null) => throw NotNeeded();
}
