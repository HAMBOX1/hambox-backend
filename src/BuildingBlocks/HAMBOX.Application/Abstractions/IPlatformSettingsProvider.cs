using HAMBOX.Application.PlatformSettings;

namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Read-only access to persisted platform settings with in-memory caching.
/// </summary>
public interface IPlatformSettingsProvider
{
    Task<T> GetAsync<T>(string categoryKey, CancellationToken cancellationToken = default);

    Task<GeneralSettingsPayload> GetGeneralAsync(CancellationToken cancellationToken = default);

    Task<BrandingSettingsPayload> GetBrandingAsync(CancellationToken cancellationToken = default);

    Task<EmailSettingsPayload> GetEmailAsync(CancellationToken cancellationToken = default);

    Task<AuthenticationSettingsPayload> GetAuthenticationAsync(CancellationToken cancellationToken = default);

    Task<SecuritySettingsPayload> GetSecurityAsync(CancellationToken cancellationToken = default);

    Task<OtpSettingsPayload> GetOtpAsync(CancellationToken cancellationToken = default);

    Task<InventorySettingsPayload> GetInventoryAsync(CancellationToken cancellationToken = default);

    Task<CurrencySettingsPayload> GetCurrencyAsync(CancellationToken cancellationToken = default);

    Task<MediaSettingsPayload> GetMediaAsync(CancellationToken cancellationToken = default);

    Task<MaintenanceSettingsPayload> GetMaintenanceAsync(CancellationToken cancellationToken = default);

    Task<StorefrontContentSettingsPayload> GetStorefrontAsync(CancellationToken cancellationToken = default);

    Task<PublicPlatformSettingsDto> GetPublicSettingsAsync(CancellationToken cancellationToken = default);

    void InvalidateCache(string? categoryKey = null);
}
