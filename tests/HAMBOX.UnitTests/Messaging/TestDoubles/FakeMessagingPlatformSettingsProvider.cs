using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;

namespace HAMBOX.UnitTests.Messaging.TestDoubles;

/// <summary>Only <see cref="GetEmailAsync"/> is exercised (the bot reads <c>ApplicationBaseUrl</c> from
/// it to build deep links) — everything else throws.</summary>
internal sealed class FakeMessagingPlatformSettingsProvider : IPlatformSettingsProvider
{
    private static NotSupportedException NotNeeded() => new("Not needed by these tests.");

    public Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default) => throw NotNeeded();
    public Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default) => throw NotNeeded();

    public Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new EmailSettingsPayload(
            true, "localhost", 1025, null, null, false, "HAMBOX", "noreply@hambox.local",
            "https://hambox.test", "/verify", "/reset-password", false));

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
