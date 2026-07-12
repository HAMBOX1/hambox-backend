using HAMBOX.Modules.Themes.Application.Abstractions;
using HAMBOX.Modules.Themes.Application.Contracts.Themes;
using HAMBOX.Modules.Themes.Domain.Themes;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Themes.Application.Services;

public interface IThemeEngine
{
    Task<ActiveThemeDto?> ResolveActiveThemeAsync(
        string? userId,
        string? membershipPlanSlug,
        DateTime? asOfUtc = null,
        CancellationToken cancellationToken = default);

    Task<ActiveThemeDto?> ResolvePreviewAsync(string previewToken, CancellationToken cancellationToken = default);
}

public sealed class ThemeEngine : IThemeEngine
{
    private readonly IThemesDbContext _dbContext;

    public ThemeEngine(IThemesDbContext dbContext) => _dbContext = dbContext;

    public async Task<ActiveThemeDto?> ResolvePreviewAsync(string previewToken, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ThemePreviewSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Token == previewToken, cancellationToken);

        if (session is null || !session.IsValid(DateTime.UtcNow))
        {
            return null;
        }

        return await BuildActiveThemeAsync(session.ThemeId, session.VersionId, "preview", cancellationToken);
    }

    public async Task<ActiveThemeDto?> ResolveActiveThemeAsync(
        string? userId,
        string? membershipPlanSlug,
        DateTime? asOfUtc = null,
        CancellationToken cancellationToken = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;

        var scheduledThemeId = await ResolveScheduledThemeIdAsync(now, cancellationToken);
        if (scheduledThemeId.HasValue)
        {
            var scheduled = await BuildPublishedThemeAsync(scheduledThemeId.Value, "schedule", cancellationToken);
            if (scheduled is not null)
            {
                return scheduled;
            }
        }

        if (!string.IsNullOrWhiteSpace(membershipPlanSlug))
        {
            var membershipThemeId = await ResolveAssignmentThemeIdAsync(
                ThemeAssignmentType.Membership,
                membershipPlanSlug,
                cancellationToken);

            if (membershipThemeId.HasValue)
            {
                var membershipTheme = await BuildPublishedThemeAsync(membershipThemeId.Value, "membership", cancellationToken);
                if (membershipTheme is not null)
                {
                    return membershipTheme;
                }
            }
        }

        var storeThemeId = await ResolveAssignmentThemeIdAsync(ThemeAssignmentType.Store, "default", cancellationToken);
        if (storeThemeId.HasValue)
        {
            var storeTheme = await BuildPublishedThemeAsync(storeThemeId.Value, "store", cancellationToken);
            if (storeTheme is not null)
            {
                return storeTheme;
            }
        }

        var defaultTheme = await _dbContext.StoreThemes
            .AsNoTracking()
            .Where(t => t.IsDefault && !t.IsDeleted && t.Status == StoreThemeStatus.Published)
            .FirstOrDefaultAsync(cancellationToken);

        if (defaultTheme?.PublishedVersionId is Guid versionId)
        {
            return await BuildActiveThemeAsync(defaultTheme.Id, versionId, "default", cancellationToken);
        }

        return null;
    }

    private async Task<Guid?> ResolveScheduledThemeIdAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        return await _dbContext.ThemeSchedules
            .AsNoTracking()
            .Where(s => s.IsActive && s.StartsAtUtc <= utcNow && (s.EndsAtUtc == null || s.EndsAtUtc >= utcNow))
            .OrderByDescending(s => s.StartsAtUtc)
            .Select(s => (Guid?)s.ThemeId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveAssignmentThemeIdAsync(
        ThemeAssignmentType type,
        string targetKey,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ThemeAssignments
            .AsNoTracking()
            .Where(a => a.IsActive && a.AssignmentType == type && a.TargetKey == targetKey)
            .OrderByDescending(a => a.Priority)
            .Select(a => (Guid?)a.ThemeId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ActiveThemeDto?> BuildPublishedThemeAsync(
        Guid themeId,
        string source,
        CancellationToken cancellationToken)
    {
        var theme = await _dbContext.StoreThemes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == themeId && !t.IsDeleted && t.Status == StoreThemeStatus.Published, cancellationToken);

        if (theme?.PublishedVersionId is not Guid versionId)
        {
            return null;
        }

        return await BuildActiveThemeAsync(themeId, versionId, source, cancellationToken);
    }

    private async Task<ActiveThemeDto?> BuildActiveThemeAsync(
        Guid themeId,
        Guid versionId,
        string source,
        CancellationToken cancellationToken)
    {
        var theme = await _dbContext.StoreThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == themeId, cancellationToken);
        var version = await _dbContext.ThemeVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (theme is null || version is null)
        {
            return null;
        }

        var assets = await _dbContext.ThemeAssets
            .AsNoTracking()
            .Where(a => a.ThemeId == themeId)
            .Select(a => new ThemeAssetDto(a.AssetType.ToString(), a.Url, a.AltText))
            .ToListAsync(cancellationToken);

        return new ActiveThemeDto(
            theme.Id,
            theme.Name,
            theme.Slug,
            theme.BaseMode.ToString(),
            version.Id,
            version.VersionNumber,
            version.GetTokens(),
            assets,
            source);
    }
}
