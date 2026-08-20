using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;

namespace HAMBOX.UnitTests.Commerce.Dot.TestDoubles;

/// <summary>
/// Supports exactly the two categories the DOT checkout/finalize path actually reads
/// (<c>commerce</c> for the reservation-window minutes, <c>referral</c> so
/// <see cref="HAMBOX.Modules.Commerce.Application.Referrals.ReferralLifecycleService"/> can no-op
/// cleanly) — everything else throws so an unexpected call fails loudly.
/// </summary>
internal sealed class FakeDotPlatformSettingsProvider : IPlatformSettingsProvider
{
    public int ReservationTimeoutMinutes { get; set; } = 30;

    public Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default)
    {
        object payload = categoryKey switch
        {
            PlatformSettingsCategoryKeys.Commerce => new CommerceSettingsPayload(0m, false, ReservationTimeoutMinutes, 0, 0, "INV"),
            PlatformSettingsCategoryKeys.Referral => new ReferralSettingsPayload(false, 0, 0m, 0),
            _ => throw new NotSupportedException($"Not needed by DOT tests: {categoryKey}."),
        };

        return Task.FromResult((T)payload);
    }

    private static NotSupportedException NotNeeded() => new("Not needed by DOT tests.");

    public Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
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
