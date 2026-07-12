using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Options;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Admin write operations for platform settings.
/// </summary>
public interface IPlatformSettingsService : IPlatformSettingsProvider
{
    Task<IReadOnlyList<PlatformSettingsCategoryDto>> GetAllCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<PlatformSettingsCategoryDto> GetCategoryAsync(
        string categoryKey,
        CancellationToken cancellationToken = default);

    Task<PlatformSettingsCategoryDto> UpdateCategoryAsync(
        string categoryKey,
        string payloadJson,
        string? actorUserId,
        string? actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<PlatformSettingsCategoryDto> RestoreDefaultsAsync(
        string categoryKey,
        string? actorUserId,
        string? actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformSettingsAuditEntryDto>> GetAuditLogAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task SendTestEmailAsync(
        string testRecipient,
        CancellationToken cancellationToken = default);

    Task<EmailSettings> GetEmailSettingsForLegacyAsync(CancellationToken cancellationToken = default);
}
