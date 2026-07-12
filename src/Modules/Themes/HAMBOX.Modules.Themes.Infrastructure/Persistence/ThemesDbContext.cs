using HAMBOX.Modules.Themes.Application.Abstractions;
using HAMBOX.Modules.Themes.Domain.Themes;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Themes.Infrastructure.Persistence;

public sealed class ThemesDbContext(DbContextOptions<ThemesDbContext> options)
    : DbContext(options), IThemesDbContext
{
    public DbSet<StoreTheme> StoreThemes => Set<StoreTheme>();
    public DbSet<ThemeVersion> ThemeVersions => Set<ThemeVersion>();
    public DbSet<ThemeSchedule> ThemeSchedules => Set<ThemeSchedule>();
    public DbSet<ThemeAssignment> ThemeAssignments => Set<ThemeAssignment>();
    public DbSet<ThemeAsset> ThemeAssets => Set<ThemeAsset>();
    public DbSet<ThemeAuditLog> ThemeAuditLogs => Set<ThemeAuditLog>();
    public DbSet<ThemePreviewSession> ThemePreviewSessions => Set<ThemePreviewSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("platform");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThemesDbContext).Assembly);

        modelBuilder.Entity<StoreTheme>().HasQueryFilter(t => !t.IsDeleted);
    }
}
