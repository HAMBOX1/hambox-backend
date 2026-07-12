using HAMBOX.Modules.Themes.Domain.Themes;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Themes.Application.Abstractions;

public interface IThemesDbContext
{
    DbSet<StoreTheme> StoreThemes { get; }
    DbSet<ThemeVersion> ThemeVersions { get; }
    DbSet<ThemeSchedule> ThemeSchedules { get; }
    DbSet<ThemeAssignment> ThemeAssignments { get; }
    DbSet<ThemeAsset> ThemeAssets { get; }
    DbSet<ThemeAuditLog> ThemeAuditLogs { get; }
    DbSet<ThemePreviewSession> ThemePreviewSessions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
