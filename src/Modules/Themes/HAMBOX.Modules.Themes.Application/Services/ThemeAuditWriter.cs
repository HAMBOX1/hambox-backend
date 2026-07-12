using HAMBOX.Modules.Themes.Application.Abstractions;
using HAMBOX.Modules.Themes.Domain.Themes;

namespace HAMBOX.Modules.Themes.Application.Services;

public static class ThemeAuditWriter
{
    public static void Record(
        IThemesDbContext dbContext,
        Guid themeId,
        ThemeAuditAction action,
        string? actorUserId,
        string? detailsJson = null)
    {
        dbContext.ThemeAuditLogs.Add(ThemeAuditLog.Create(themeId, action, actorUserId, detailsJson));
    }
}
